using System.Collections.Concurrent;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

/// <summary>
/// In-process gate enforcing the configured parallelism of each lane.
/// </summary>
/// <remarks>
/// Counts are held per process. K7 is expected to run as a single instance against a given database;
/// see docs/admin/operating.md. Supporting several instances would require moving these counters to the
/// database (Postgres advisory locks would do; SQLite has no equivalent).
/// </remarks>
internal static class BackgroundTaskConcurrencyGate
{
    /// <summary>
    /// Builds the gate key of a task. Federation work is isolated per peer so one slow peer cannot
    /// starve the others, without adding one lane per peer.
    /// </summary>
    /// <param name="lane">Lane of the task.</param>
    /// <param name="federationPeerId">Peer the task belongs to, when relevant.</param>
    public static string BuildKey(BackgroundTaskLane lane, Guid? federationPeerId)
        => lane == BackgroundTaskLane.Federation && federationPeerId.HasValue
            ? $"{lane}:{federationPeerId.Value}"
            : lane.ToString();

    public static bool TryAcquire(ConcurrentDictionary<string, int> activeCountByKey, string key, int limit)
    {
        if (limit <= 0)
            return false;

        while (true)
        {
            var current = activeCountByKey.GetOrAdd(key, 0);
            if (current >= limit)
                return false;

            if (activeCountByKey.TryUpdate(key, current + 1, current))
                return true;
        }
    }

    public static void Release(ConcurrentDictionary<string, int> activeCountByKey, string key)
        => activeCountByKey.AddOrUpdate(key, 0, (_, count) => Math.Max(0, count - 1));
}
