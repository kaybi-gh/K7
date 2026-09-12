using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Interfaces;

public interface IScrobbleDestination
{
    ScrobblerProvider Provider { get; }

    Task<ScrobbleSendResult> SendAsync(
        UserScrobblerAccount account,
        string configJson,
        ScrobblePayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Connectivity check for the account. Defaults to a sample SendAsync payload.
    /// </summary>
    Task<ScrobbleSendResult> TestAsync(
        UserScrobblerAccount account,
        string configJson,
        CancellationToken cancellationToken = default) =>
        SendAsync(account, configJson, ScrobblePayload.CreateTestSample(account.UserId), cancellationToken);
}

public sealed record ScrobbleSendResult(bool Success, string? Error = null)
{
    public static ScrobbleSendResult Ok() => new(true);

    public static ScrobbleSendResult Fail(string error) => new(false, error);
}

public sealed record ScrobblePayload
{
    public required Guid UserId { get; init; }
    public required Guid MediaId { get; init; }
    public required MediaType MediaType { get; init; }
    public required string Title { get; init; }
    public string? OriginalTitle { get; init; }
    public int? Year { get; init; }
    public string? Artist { get; init; }
    public string? Album { get; init; }
    public string? TrackName { get; init; }
    public int? TrackNumber { get; init; }
    public string? ShowName { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public string? EpisodeName { get; init; }
    public string? Tmdb { get; init; }
    public string? Imdb { get; init; }
    public string? Tvdb { get; init; }
    /// <summary>Show-level TMDb id for episodes (Yamtrack Series.ProviderIds).</summary>
    public string? ShowTmdb { get; init; }
    /// <summary>Show-level TVDB id for episodes (Yamtrack Series.ProviderIds).</summary>
    public string? ShowTvdb { get; init; }
    public string? MusicBrainz { get; init; }
    public double PositionSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public double ProgressPercent { get; init; }
    public PlaybackState State { get; init; }
    public bool IsCompleted { get; init; }
    /// <summary>
    /// True for throttled mid-playback position updates (webhook Progress event).
    /// </summary>
    public bool IsProgressTick { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? UserName { get; init; }

    public static ScrobblePayload CreateTestSample(Guid userId) => new()
    {
        UserId = userId,
        MediaId = Guid.Empty,
        MediaType = MediaType.MusicTrack,
        Title = "K7 test",
        Artist = "K7",
        Album = "Connectivity",
        TrackName = "Test track",
        Year = 2014,
        Tmdb = "550",
        Imdb = "tt0137523",
        ProgressPercent = 50,
        PositionSeconds = 120,
        DurationSeconds = 240,
        State = PlaybackState.Playing,
        IsCompleted = false,
        Timestamp = DateTimeOffset.UtcNow,
        UserName = "k7"
    };
}

public static class ScrobbleEligibility
{
    public static bool MeetsListenThreshold(double progressPercent, double durationSeconds)
    {
        var halfway = progressPercent >= 50;
        var fourMinutes = durationSeconds > 0 && (durationSeconds * progressPercent / 100) >= 240;
        return halfway || fourMinutes;
    }

    public static bool AllowsMediaType(IReadOnlyList<MediaType> configured, MediaType mediaType)
    {
        if (configured.Count == 0)
            return true;

        return configured.Contains(mediaType);
    }
}
