using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Constants;

/// <summary>
/// Scheduling policy constants: lane parallelism defaults, score boosts and aging.
/// </summary>
/// <remarks>
/// These are product policy, deliberately not exposed in the admin UI. Only lane limits and the worker
/// count are operator-configurable.
/// <para>
/// There is no time-based aging on purpose. Ordering is work class, then schedule score, then creation
/// date, which maps exactly onto a database index; an aging term computed in code could not be
/// translated to SQL and would degrade every pick into a full sort. Deferring a low work class while
/// critical work remains is the intended behaviour, not starvation: once a scan drains, nothing
/// critical is left and polish runs. Creation date as the last key keeps the order fair inside a class.
/// </para>
/// </remarks>
public static class BackgroundTaskScheduling
{
    /// <summary>Parallelism used when an operator has not configured a lane.</summary>
    public const int DefaultLaneLimit = 1;

    /// <summary>Upper bound accepted for a configured lane limit.</summary>
    public const int MaxLaneLimit = 32;

    /// <summary>Default number of workers picking from the queue.</summary>
    public const int DefaultWorkerCount = 3;

    /// <summary>Upper bound accepted for the worker count.</summary>
    public const int MaxWorkerCount = 32;

    /// <summary>Clamp operator-configured worker count. Zero pauses all background processing.</summary>
    public static int ClampWorkerCount(int value) => Math.Clamp(value, 0, MaxWorkerCount);

    /// <summary>Clamp operator-configured lane limit. Zero pauses the lane.</summary>
    public static int ClampLaneLimit(int value) => Math.Clamp(value, 0, MaxLaneLimit);

    /// <summary>
    /// Score added when a user explicitly asks for something. Large enough to clear any realistic
    /// aging accumulated by a first-index backlog.
    /// </summary>
    public const int InteractiveBoost = 1_000_000;

    /// <summary>
    /// Score added to the pending tasks of a media a user just asked to play or opened. Smaller than
    /// <see cref="InteractiveBoost"/> so an explicit action still wins over a passive visit.
    /// </summary>
    public const int OnDemandBoost = 100_000;

    /// <summary>
    /// Number of candidates fetched per pick. Taking more than one lets a worker fall through to the
    /// next eligible task when a lane saturates between the query and the gate acquisition, instead of
    /// giving up and waiting for the next wake-up.
    /// </summary>
    public const int CandidateFetchCount = 24;

    /// <summary>
    /// How many times a task may be reclaimed after its worker vanished before it is failed. Guards
    /// against a task that repeatedly kills the process looping forever.
    /// </summary>
    public const int MaxReclaims = 3;

    /// <summary>
    /// Hard parallelism per logical external provider on the Metadata lane. Not operator-configurable:
    /// HTTP pacing stays on <c>OutboundRateLimiter</c>; this only prevents stacking tasks on one provider.
    /// </summary>
    public const int MetadataProviderLimit = 1;

    private static readonly Dictionary<BackgroundTaskLane, int> DefaultLimits = new()
    {
        [BackgroundTaskLane.Probe] = 4,
        [BackgroundTaskLane.ImageProcessing] = 2,
        // High enough that the usual brake is 1/provider + rate limits, not this ceiling.
        [BackgroundTaskLane.Metadata] = 8
    };

    /// <summary>
    /// Default parallelism of a lane. Probing defaults at or above <see cref="DefaultWorkerCount"/> so
    /// the probes of a scan batch drain before the media creation tasks of the same batch obtain a
    /// worker, which makes a media playable by the time it becomes visible.
    /// </summary>
    /// <param name="lane">The lane to resolve.</param>
    public static int GetDefaultLimit(BackgroundTaskLane lane)
        => DefaultLimits.TryGetValue(lane, out var limit) ? limit : DefaultLaneLimit;
}
