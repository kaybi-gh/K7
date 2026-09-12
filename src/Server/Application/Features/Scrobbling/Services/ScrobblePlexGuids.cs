using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Scrobbling.Services;

/// <summary>
/// Builds Plex webhook payloads for BetaSeries.
/// Movies require IMDb. Episodes require episode-level TVDB.
/// Jellyfin Generic Form posts these as application/x-www-form-urlencoded field "payload".
/// </summary>
public static class ScrobblePlexGuids
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public static bool HasRequiredIds(ScrobblePayload payload) => payload.MediaType switch
    {
        MediaType.Movie => !string.IsNullOrWhiteSpace(payload.Imdb),
        MediaType.SerieEpisode => !string.IsNullOrWhiteSpace(payload.Tvdb),
        _ => false
    };

    public static string? ToPlexEventName(string? webhookEventKey) => webhookEventKey switch
    {
        ScrobbleWebhookEvents.Scrobble => "media.scrobble",
        ScrobbleWebhookEvents.Play or ScrobbleWebhookEvents.Progress => "media.play",
        ScrobbleWebhookEvents.Pause => "media.pause",
        ScrobbleWebhookEvents.Stop => "media.stop",
        _ => null
    };

    public static string BuildFormPayloadJson(ScrobblePayload payload, string plexEvent)
    {
        var metadata = payload.MediaType == MediaType.SerieEpisode
            ? BuildEpisodeMetadata(payload)
            : BuildMovieMetadata(payload);

        var root = new Dictionary<string, object?>
        {
            ["event"] = plexEvent,
            ["user"] = true,
            ["owner"] = true,
            ["Account"] = new Dictionary<string, object?>
            {
                ["id"] = 1,
                ["thumb"] = "",
                ["title"] = string.IsNullOrWhiteSpace(payload.UserName) ? "k7" : payload.UserName
            },
            ["Server"] = new Dictionary<string, object?>
            {
                ["title"] = "K7",
                ["uuid"] = "k7"
            },
            ["Player"] = new Dictionary<string, object?>
            {
                ["local"] = true,
                ["publicAddress"] = "127.0.0.1",
                ["title"] = "K7",
                ["uuid"] = "k7"
            },
            ["Metadata"] = metadata
        };

        return JsonSerializer.Serialize(root, JsonOptions);
    }

    public static ScrobblePayload CreateBetaSeriesTestSample(Guid userId) => new()
    {
        UserId = userId,
        MediaId = Guid.Empty,
        MediaType = MediaType.Movie,
        Title = "Fight Club",
        Year = 1999,
        Tmdb = "550",
        Imdb = "tt0137523",
        ProgressPercent = 95,
        PositionSeconds = 8300,
        DurationSeconds = 8400,
        State = PlaybackState.Ended,
        IsCompleted = true,
        Timestamp = DateTimeOffset.UtcNow,
        UserName = "k7"
    };

    private static Dictionary<string, object?> BuildMovieMetadata(ScrobblePayload payload)
    {
        var guids = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(payload.Imdb))
            guids.Add(new Dictionary<string, string> { ["id"] = $"imdb://{payload.Imdb}" });
        if (!string.IsNullOrWhiteSpace(payload.Tmdb))
            guids.Add(new Dictionary<string, string> { ["id"] = $"tmdb://{payload.Tmdb}" });

        return new Dictionary<string, object?>
        {
            ["librarySectionType"] = "movie",
            ["librarySectionID"] = 2,
            ["ratingKey"] = "1",
            ["key"] = "/library/metadata/1",
            ["type"] = "movie",
            ["title"] = payload.Title,
            ["year"] = payload.Year,
            ["guid"] = LegacyGuid(payload),
            ["Guid"] = guids
        };
    }

    private static Dictionary<string, object?> BuildEpisodeMetadata(ScrobblePayload payload)
    {
        var guids = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(payload.Tvdb))
            guids.Add(new Dictionary<string, string> { ["id"] = $"tvdb://{payload.Tvdb}" });
        if (!string.IsNullOrWhiteSpace(payload.Tmdb))
            guids.Add(new Dictionary<string, string> { ["id"] = $"tmdb://{payload.Tmdb}" });

        return new Dictionary<string, object?>
        {
            ["librarySectionType"] = "show",
            ["librarySectionID"] = 1,
            ["ratingKey"] = "1",
            ["key"] = "/library/metadata/1",
            ["parentRatingKey"] = "2",
            ["grandparentRatingKey"] = "3",
            ["type"] = "episode",
            ["title"] = payload.Title,
            ["grandparentTitle"] = payload.ShowName,
            ["parentTitle"] = payload.SeasonNumber is null ? null : $"Season {payload.SeasonNumber}",
            ["parentIndex"] = payload.SeasonNumber,
            ["index"] = payload.EpisodeNumber,
            ["Guid"] = guids
        };
    }

    public static string LegacyGuid(ScrobblePayload payload)
    {
        if (payload.MediaType == MediaType.Movie && !string.IsNullOrWhiteSpace(payload.Imdb))
            return $"com.plexapp.agents.imdb://{payload.Imdb}?lang=en";

        return "";
    }
}
