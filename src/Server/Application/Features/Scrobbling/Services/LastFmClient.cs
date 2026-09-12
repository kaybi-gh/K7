using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Services;

public class LastFmClient(IHttpClientFactory httpClientFactory, IServerSettingsService settings)
{
    public async Task<LastFmAuthStartDto> StartAuthAsync(CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        EnsureConfigured(config);

        var token = await CallAsync(config, new Dictionary<string, string>
        {
            ["method"] = "auth.getToken"
        }, cancellationToken);

        var value = token.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Last.fm did not return a token");

        return new LastFmAuthStartDto
        {
            Token = value,
            AuthUrl = $"https://www.last.fm/api/auth/?api_key={Uri.EscapeDataString(config.LastFmApiKey)}&token={Uri.EscapeDataString(value)}"
        };
    }

    public async Task<(string SessionKey, string Username)> GetSessionAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        EnsureConfigured(config);
        var json = await CallAsync(config, new Dictionary<string, string>
        {
            ["method"] = "auth.getSession",
            ["token"] = token
        }, cancellationToken);

        var session = json.GetProperty("session");
        return (
            session.GetProperty("key").GetString() ?? "",
            session.GetProperty("name").GetString() ?? "");
    }

    public async Task<bool> UpdateNowPlayingAsync(
        string sessionKey,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        var args = TrackArgs(payload);
        args["method"] = "track.updateNowPlaying";
        args["sk"] = sessionKey;
        await CallAsync(config, args, cancellationToken);
        return true;
    }

    public async Task<bool> ScrobbleAsync(
        string sessionKey,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        var args = TrackArgs(payload);
        args["method"] = "track.scrobble";
        args["sk"] = sessionKey;
        args["timestamp"] = payload.Timestamp.ToUnixTimeSeconds().ToString();
        await CallAsync(config, args, cancellationToken);
        return true;
    }

    public async Task<bool> TestSessionAsync(string sessionKey, CancellationToken cancellationToken = default)
    {
        var config = await LoadSettings(cancellationToken);
        await CallAsync(config, new Dictionary<string, string>
        {
            ["method"] = "user.getInfo",
            ["sk"] = sessionKey
        }, cancellationToken);
        return true;
    }

    private static Dictionary<string, string> TrackArgs(ScrobblePayload payload) => new()
    {
        ["artist"] = payload.Artist ?? payload.ShowName ?? payload.Title,
        ["track"] = payload.TrackName ?? payload.EpisodeName ?? payload.Title,
        ["album"] = payload.Album ?? ""
    };

    private async Task<ScrobblingSettingsDto> LoadSettings(CancellationToken cancellationToken)
    {
        var json = await settings.GetAsync(ServerSettingKeys.Scrobbling, cancellationToken);
        return string.IsNullOrEmpty(json)
            ? new ScrobblingSettingsDto()
            : JsonSerializer.Deserialize<ScrobblingSettingsDto>(json) ?? new ScrobblingSettingsDto();
    }

    private static void EnsureConfigured(ScrobblingSettingsDto config)
    {
        if (string.IsNullOrWhiteSpace(config.LastFmApiKey) || string.IsNullOrWhiteSpace(config.LastFmApiSecret))
        {
            throw new ValidationException(
            [
                new ValidationFailure("LastFm", "Last.fm is not configured. Contact your administrator.")
            ]);
        }
    }

    private async Task<JsonElement> CallAsync(
        ScrobblingSettingsDto config,
        Dictionary<string, string> args,
        CancellationToken cancellationToken)
    {
        args["api_key"] = config.LastFmApiKey;
        args["format"] = "json";
        args["api_sig"] = Sign(args, config.LastFmApiSecret);

        var host = string.IsNullOrWhiteSpace(config.LastFmApiHost)
            ? "https://ws.audioscrobbler.com/2.0/"
            : config.LastFmApiHost;

        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        using var response = await client.PostAsync(host, new FormUrlEncodedContent(args), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            var code = error.ToString();
            var message = doc.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString()
                : null;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? $"Last.fm error {code}"
                    : $"Last.fm error {code}: {message}");
        }

        return doc.RootElement.Clone();
    }

    private static string Sign(Dictionary<string, string> args, string secret)
    {
        var raw = string.Concat(args
            .Where(p => p.Key != "format" && p.Key != "api_sig")
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Key + p.Value)) + secret;
#pragma warning disable CA5351
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
#pragma warning restore CA5351
    }
}
