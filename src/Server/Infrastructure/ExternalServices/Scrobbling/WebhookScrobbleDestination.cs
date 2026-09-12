using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Infrastructure.ExternalServices.Scrobbling;

public sealed class WebhookScrobbleDestination(
    IHttpClientFactory httpClientFactory,
    NotificationPayloadRenderer renderer) : IScrobbleDestination
{
    public ScrobblerProvider Provider => ScrobblerProvider.Webhook;

    private HttpClient CreateClient() =>
        httpClientFactory.CreateClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient);

    public Task<ScrobbleSendResult> TestAsync(
        UserScrobblerAccount account,
        string configJson,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<WebhookConfig>(configJson);
        // Connectivity only: never send media.scrobble. BetaSeries ignores media.play for watched.
        var sample = IsBetaSeries(ScrobbleWebhookPresets.Find(config?.PresetId), config?.PresetId)
            ? ScrobblePlexGuids.CreateBetaSeriesTestSample(account.UserId) with
            {
                State = PlaybackState.Playing,
                IsCompleted = false,
                ProgressPercent = 5,
                PositionSeconds = 30
            }
            : ScrobblePayload.CreateTestSample(account.UserId);

        return SendAsync(account, configJson, sample, cancellationToken);
    }

    public async Task<ScrobbleSendResult> SendAsync(
        UserScrobblerAccount account,
        string configJson,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default)
    {
        var config = ScrobbleJson.Deserialize<WebhookConfig>(configJson);
        if (config is null || string.IsNullOrWhiteSpace(config.Url))
            return ScrobbleSendResult.Fail("Webhook URL is missing.");

        var eventKey = ScrobbleWebhookEvents.ForPlaybackState(
            payload.State, payload.IsCompleted, payload.IsProgressTick);
        if (!ScrobbleWebhookEvents.IsEnabled(config.Events, eventKey))
            return ScrobbleSendResult.Ok();

        var preset = ScrobbleWebhookPresets.Find(config.PresetId);
        if (IsBetaSeries(preset, config.PresetId))
            return await SendBetaSeriesAsync(config, payload, eventKey, cancellationToken);

        if (IsJellyfinVideoPreset(preset)
            && payload.MediaType == MediaType.SerieEpisode
            && (payload.SeasonNumber is null || payload.EpisodeNumber is null))
        {
            return ScrobbleSendResult.Fail(
                "Episode scrobble requires season and episode numbers (Yamtrack rejects null indexes).");
        }

        var template = ResolveTemplate(eventKey, config, preset);
        if (string.IsNullOrWhiteSpace(template))
            return ScrobbleSendResult.Fail($"Webhook template is missing for event '{eventKey}'.");

        string body;
        try
        {
            var data = ToDictionary(payload);
            body = renderer.Render(template, data);
        }
        catch (Exception ex)
        {
            return ScrobbleSendResult.Fail($"Webhook template render failed: {ex.Message}");
        }

        try
        {
            var client = CreateClient();
            using var request = new HttpRequestMessage(new HttpMethod(config.Method ?? "POST"), config.Url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            using var response = await client.SendAsync(request, cancellationToken);
            return await ScrobbleHttp.FromResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return ScrobbleSendResult.Fail(ex.Message);
        }
    }

    private async Task<ScrobbleSendResult> SendBetaSeriesAsync(
        WebhookConfig config,
        ScrobblePayload payload,
        string? eventKey,
        CancellationToken cancellationToken)
    {
        var plexEvent = ScrobblePlexGuids.ToPlexEventName(eventKey);
        if (plexEvent is null)
            return ScrobbleSendResult.Ok();

        // BetaSeries only marks watched on media.scrobble. Other events are no-ops but still valid POSTs.
        if (plexEvent == "media.scrobble" && !ScrobblePlexGuids.HasRequiredIds(payload))
        {
            return ScrobbleSendResult.Fail(
                payload.MediaType == MediaType.Movie
                    ? "BetaSeries movie scrobble requires an IMDb id on the media."
                    : "BetaSeries episode scrobble requires a TVDB id on the media.");
        }

        // Match Jellyfin Generic Form: form field "payload" = inner Plex JSON (not application/json body).
        var innerJson = ScrobblePlexGuids.BuildFormPayloadJson(payload, plexEvent);

        try
        {
            var client = CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, config.Url)
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("payload", innerJson)
                ])
            };

            using var response = await client.SendAsync(request, cancellationToken);
            return await ScrobbleHttp.FromResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            return ScrobbleSendResult.Fail(ex.Message);
        }
    }

    private static bool IsJellyfinVideoPreset(ScrobbleWebhookPresetDto? preset) =>
        preset?.Id is "yamtrack" or "floppy" or "ryot";

    private static string? ResolveTemplate(
        string? eventKey,
        WebhookConfig config,
        ScrobbleWebhookPresetDto? preset)
    {
        // Non-custom presets always use live templates so account configs stay current after preset fixes.
        if (preset is not null
            && !string.Equals(preset.Id, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return eventKey switch
            {
                ScrobbleWebhookEvents.Play => preset.PlayTemplate,
                ScrobbleWebhookEvents.Pause => preset.PauseTemplate,
                ScrobbleWebhookEvents.Stop => preset.StopTemplate,
                ScrobbleWebhookEvents.Progress => preset.ProgressTemplate,
                ScrobbleWebhookEvents.Scrobble => preset.ScrobbleTemplate,
                _ => null
            };
        }

        return eventKey switch
        {
            ScrobbleWebhookEvents.Play => config.PlayTemplate ?? config.ProgressTemplate,
            ScrobbleWebhookEvents.Pause => config.PauseTemplate ?? config.ProgressTemplate,
            ScrobbleWebhookEvents.Stop => config.StopTemplate ?? config.ProgressTemplate,
            ScrobbleWebhookEvents.Progress => config.ProgressTemplate ?? config.PlayTemplate,
            ScrobbleWebhookEvents.Scrobble => config.ScrobbleTemplate ?? config.StopTemplate ?? config.ProgressTemplate,
            _ => null
        };
    }

    private static bool IsBetaSeries(ScrobbleWebhookPresetDto? preset, string? presetId) =>
        string.Equals(preset?.Id ?? presetId, "betaseries", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, object?> ToDictionary(ScrobblePayload payload) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["UserId"] = payload.UserId,
        ["UserName"] = payload.UserName,
        ["MediaId"] = payload.MediaId,
        ["Title"] = payload.Title,
        ["OriginalTitle"] = payload.OriginalTitle,
        ["MediaType"] = payload.MediaType.ToString(),
        ["Year"] = payload.Year,
        ["Artist"] = payload.Artist,
        ["Album"] = payload.Album,
        ["TrackName"] = payload.TrackName,
        ["TrackNumber"] = payload.TrackNumber,
        ["ShowName"] = payload.ShowName,
        ["SeasonNumber"] = payload.SeasonNumber,
        ["EpisodeNumber"] = payload.EpisodeNumber,
        ["EpisodeName"] = payload.EpisodeName,
        ["Tmdb"] = payload.Tmdb,
        ["Imdb"] = payload.Imdb,
        ["Tvdb"] = payload.Tvdb,
        ["ShowTmdb"] = payload.ShowTmdb,
        ["ShowTvdb"] = payload.ShowTvdb,
        ["MusicBrainz"] = payload.MusicBrainz,
        ["PlexLegacyGuid"] = ScrobblePlexGuids.LegacyGuid(payload),
        ["ProgressPercent"] = payload.ProgressPercent,
        ["PositionSeconds"] = payload.PositionSeconds,
        ["DurationSeconds"] = payload.DurationSeconds,
        ["PositionTicks"] = (long)(payload.PositionSeconds * 10_000_000),
        ["DurationTicks"] = (long)(payload.DurationSeconds * 10_000_000),
        ["State"] = payload.State.ToString(),
        ["IsCompleted"] = payload.IsCompleted,
        ["Timestamp"] = payload.Timestamp.ToString("O")
    };

    private sealed class WebhookConfig
    {
        public string? Url { get; set; }
        public string? Method { get; set; }
        public string? PresetId { get; set; }
        public List<string>? Events { get; set; }
        public string? PlayTemplate { get; set; }
        public string? PauseTemplate { get; set; }
        public string? StopTemplate { get; set; }
        public string? ProgressTemplate { get; set; }
        public string? ScrobbleTemplate { get; set; }
    }
}
