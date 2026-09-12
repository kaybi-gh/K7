using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

public sealed class ListenBrainzScrobbleDestination(
    IHttpClientFactory httpClientFactory,
    ILogger<ListenBrainzScrobbleDestination> logger) : IScrobbleDestination
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly TimeSpan MaxRateLimitWait = TimeSpan.FromSeconds(60);
    private const int MaxAttempts = 3;

    public ScrobblerProvider Provider => ScrobblerProvider.ListenBrainz;

    public async Task<ScrobbleSendResult> TestAsync(
        UserScrobblerAccount account,
        string configJson,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<ListenBrainzConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.Token))
            return ScrobbleSendResult.Fail("ListenBrainz token is missing. Reconnect the account.");

        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.listenbrainz.org/1/validate-token");
        request.Headers.TryAddWithoutValidation("Authorization", $"Token {config.Token.Trim()}");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return await ScrobbleHttp.FromResponseAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (doc.RootElement.TryGetProperty("valid", out var valid) && valid.ValueKind == JsonValueKind.True)
            return ScrobbleSendResult.Ok();

        var message = doc.RootElement.TryGetProperty("message", out var msg)
            && msg.ValueKind == JsonValueKind.String
            ? msg.GetString()
            : null;

        return ScrobbleSendResult.Fail(
            string.IsNullOrWhiteSpace(message) ? "ListenBrainz token is invalid." : message);
    }

    public async Task<ScrobbleSendResult> SendAsync(
        UserScrobblerAccount account,
        string configJson,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<ListenBrainzConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.Token))
            return ScrobbleSendResult.Fail("ListenBrainz token is missing. Reconnect the account.");

        // ListenBrainz only accepts single (completed listen) and playing_now.
        // Skip pause/stop/buffering ticks that are not completions.
        var isSingle = payload.IsCompleted || payload.State is PlaybackState.Ended;
        if (!isSingle && payload.State is not PlaybackState.Playing)
            return ScrobbleSendResult.Ok();

        // playing_now is advisory: do not spam LB when play/progress flaps.
        if (!isSingle && !ListenBrainzPlayingNowThrottle.Instance.TryAcquire(account.Id))
        {
            logger.LogDebug(
                "ListenBrainz playing_now throttled for account {AccountId}",
                account.Id);
            return ScrobbleSendResult.Ok();
        }

        var listenType = isSingle ? "single" : "playing_now";
        var listen = new Dictionary<string, object?>
        {
            ["track_metadata"] = BuildTrackMetadata(payload)
        };
        if (isSingle)
            listen["listened_at"] = payload.Timestamp.ToUnixTimeSeconds();

        var body = new Dictionary<string, object?>
        {
            ["listen_type"] = listenType,
            ["payload"] = new object[] { listen }
        };

        // Buffer as StringContent: StandardResilienceHandler can send JsonContent with an
        // empty body (ListenBrainz then returns "zero-length, empty document").
        var json = JsonSerializer.Serialize(body, SerializerOptions);
        var client = httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);
        var token = config.Token.Trim();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.listenbrainz.org/1/submit-listens");
            request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return ScrobbleSendResult.Ok();

            var delay = ScrobbleRateLimit.TryGetDelay(response, MaxRateLimitWait);
            if (delay is null || attempt >= MaxAttempts)
                return await ScrobbleHttp.FromResponseAsync(response, cancellationToken);

            logger.LogInformation(
                "ListenBrainz rate limited for account {AccountId}, waiting {Delay}s (attempt {Attempt}/{Max})",
                account.Id,
                delay.Value.TotalSeconds,
                attempt,
                MaxAttempts);

            await Task.Delay(delay.Value, cancellationToken);
        }

        return ScrobbleSendResult.Fail("ListenBrainz rate limit retries exhausted.");
    }

    private static Dictionary<string, object?> BuildTrackMetadata(ScrobblePayload payload)
    {
        var additional = new Dictionary<string, object?>
        {
            ["music_service"] = "k7",
            ["submission_client"] = "K7"
        };

        if (payload.DurationSeconds > 0)
            additional["duration_ms"] = (int)Math.Round(payload.DurationSeconds * 1000);

        if (!string.IsNullOrWhiteSpace(payload.MusicBrainz))
            additional["recording_mbid"] = payload.MusicBrainz;

        return new Dictionary<string, object?>
        {
            ["artist_name"] = FirstNonEmpty(payload.Artist, payload.ShowName, "Unknown"),
            ["track_name"] = FirstNonEmpty(payload.TrackName, payload.EpisodeName, payload.Title),
            ["release_name"] = string.IsNullOrWhiteSpace(payload.Album) ? null : payload.Album,
            ["additional_info"] = additional
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "Unknown";
    }

    private sealed class ListenBrainzConfig
    {
        public string? Token { get; set; }
    }
}
