namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Retry schedule for the sidecar VTT of a text subtitle track. The server answers 503 while
/// ffmpeg extracts the track, and the extraction reads the whole file: seconds for a small
/// encode, minutes for a large remux on slow storage. A client that gives up after a fixed
/// number of polls leaves the selected track silently off until the user toggles it, so the
/// wait is bounded by elapsed time instead and backs off after the first polls.
/// </summary>
public static class SubtitleVttRetryPolicy
{
    /// <summary>HTTP status the server returns while the VTT is being extracted.</summary>
    public const int ExtractionInProgressStatusCode = 503;

    /// <summary>Longest a client keeps polling one track before it logs and gives up.</summary>
    public static readonly TimeSpan MaxTotalWait = TimeSpan.FromMinutes(10);

    /// <summary>Delay between two requests while the server only has a partial VTT.</summary>
    public static readonly TimeSpan PartialRefreshInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Position change, since the last request, that normal playback cannot produce within
    /// <see cref="PartialRefreshInterval"/> at any speed: a seek.
    /// </summary>
    private const double PartialSeekThresholdSeconds = 30;

    private const int FastAttempts = 15;
    private static readonly TimeSpan FastDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SlowDelay = TimeSpan.FromSeconds(3);

    public static bool IsExtractionInProgress(int statusCode) => statusCode == ExtractionInProgressStatusCode;

    /// <summary>
    /// Delay before the next poll: one second for the first attempts (short extractions show
    /// up fast), then three seconds so a long extraction does not cost a request per second.
    /// </summary>
    public static TimeSpan DelayBeforeRetry(int attempt) => attempt < FastAttempts ? FastDelay : SlowDelay;

    public static bool ShouldGiveUp(TimeSpan elapsed) => elapsed >= MaxTotalWait;

    /// <summary>
    /// When to ask again after a partial answer (cues around the position sent with the
    /// request, see <c>SubtitleVttFormat.PartialNote</c>): at a regular interval, so the cues
    /// follow playback and the complete track replaces them once extracted, and at once after
    /// a seek, which usually lands outside the cues the client holds.
    /// </summary>
    public static bool ShouldRefreshPartial(
        double requestedPositionSeconds,
        double currentPositionSeconds,
        TimeSpan sinceRequest) =>
        sinceRequest >= PartialRefreshInterval
        || Math.Abs(currentPositionSeconds - requestedPositionSeconds) > PartialSeekThresholdSeconds;
}
