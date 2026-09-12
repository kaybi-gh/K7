using System.Net.Http.Json;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Services;

public class TraktClient(IHttpClientFactory httpClientFactory, IServerSettingsService settings)
{
    private const string BaseUrl = "https://api.trakt.tv";

    public async Task<TraktDeviceStartDto> StartDeviceAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        EnsureConfigured(config);

        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth/device/code");
        AddHeaders(request, config);
        request.Content = JsonContent.Create(new { client_id = config.TraktClientId });

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new TraktDeviceStartDto
        {
            DeviceCode = json.GetProperty("device_code").GetString() ?? "",
            UserCode = json.GetProperty("user_code").GetString() ?? "",
            VerificationUrl = json.GetProperty("verification_url").GetString() ?? "https://trakt.tv/activate",
            Interval = json.TryGetProperty("interval", out var interval) ? interval.GetInt32() : 5,
            ExpiresIn = json.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 600
        };
    }

    public async Task<(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)?> PollDeviceAsync(
        string deviceCode,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        EnsureConfigured(config);

        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/oauth/device/token");
        AddHeaders(request, config);
        request.Content = JsonContent.Create(new
        {
            code = deviceCode,
            client_id = config.TraktClientId,
            client_secret = config.TraktClientSecret
        });

        using var response = await client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 400 || (int)response.StatusCode == 418)
            return null;

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 7776000;
        return (
            json.GetProperty("access_token").GetString() ?? "",
            json.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "",
            DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    public async Task<bool> ScrobbleAsync(
        string accessToken,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        EnsureConfigured(config);

        var action = payload.State switch
        {
            PlaybackState.Playing => "start",
            PlaybackState.Paused => "pause",
            _ => "stop"
        };

        object? body = payload.MediaType switch
        {
            MediaType.Movie => new
            {
                movie = new { title = payload.Title, year = payload.Year, ids = new { tmdb = payload.Tmdb, imdb = payload.Imdb } },
                progress = payload.ProgressPercent
            },
            MediaType.SerieEpisode => new
            {
                episode = new
                {
                    title = payload.EpisodeName ?? payload.Title,
                    season = payload.SeasonNumber,
                    number = payload.EpisodeNumber,
                    ids = new { tmdb = payload.Tmdb, tvdb = payload.Tvdb, imdb = payload.Imdb }
                },
                progress = payload.ProgressPercent
            },
            _ => null
        };

        if (body is null)
            return true;

        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/scrobble/{action}");
        AddHeaders(request, config, accessToken);
        request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return true;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 240)
            detail = detail[..240] + "...";
        throw new InvalidOperationException(
            $"Trakt HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(detail) ? response.ReasonPhrase : detail)}");
    }

    public async Task<bool> TestAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        EnsureConfigured(config);

        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/users/settings");
        AddHeaders(request, config, accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return true;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 240)
            detail = detail[..240] + "...";
        throw new InvalidOperationException(
            $"Trakt HTTP {(int)response.StatusCode}: {(string.IsNullOrWhiteSpace(detail) ? response.ReasonPhrase : detail)}");
    }

    private static void AddHeaders(HttpRequestMessage request, ScrobblingSettingsDto config, string? accessToken = null)
    {
        request.Headers.TryAddWithoutValidation("trakt-api-version", "2");
        request.Headers.TryAddWithoutValidation("trakt-api-key", config.TraktClientId);
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");
    }

    private static void EnsureConfigured(ScrobblingSettingsDto config)
    {
        if (string.IsNullOrWhiteSpace(config.TraktClientId) || string.IsNullOrWhiteSpace(config.TraktClientSecret))
        {
            throw new ValidationException(
            [
                new ValidationFailure("Trakt", "Trakt is not configured. Contact your administrator.")
            ]);
        }
    }

    private async Task<ScrobblingSettingsDto> LoadSettings(CancellationToken cancellationToken)
    {
        var json = await settings.GetAsync(ServerSettingKeys.Scrobbling, cancellationToken);
        return string.IsNullOrEmpty(json)
            ? new ScrobblingSettingsDto()
            : JsonSerializer.Deserialize<ScrobblingSettingsDto>(json) ?? new ScrobblingSettingsDto();
    }
}
