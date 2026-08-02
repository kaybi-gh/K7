using System.Collections.Concurrent;
using K7.Server.Application.Common;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Services;

/// <summary>
/// In-process gate enforcing the configured parallelism of each lane (and Metadata providers).
/// </summary>
/// <remarks>
/// Counts are held per process. K7 is expected to run as a single instance against a given database;
/// see docs/admin/operating.md. Supporting several instances would require moving these counters to the
/// database (Postgres advisory locks would do; SQLite has no equivalent).
/// </remarks>
internal static class BackgroundTaskConcurrencyGate
{
    public const string MetadataKeyPrefix = "Metadata:";

    /// <summary>
    /// Builds the gate key of a task. Federation work is isolated per peer; Metadata work is isolated
    /// per logical external provider so one slow provider cannot occupy every Metadata slot.
    /// </summary>
    public static string BuildKey(
        BackgroundTaskLane lane,
        Guid? federationPeerId,
        string? metadataProviderName = null)
    {
        if (lane == BackgroundTaskLane.Federation && federationPeerId.HasValue)
            return $"{lane}:{federationPeerId.Value}";

        if (lane == BackgroundTaskLane.Metadata)
        {
            var provider = string.IsNullOrWhiteSpace(metadataProviderName)
                ? MetadataProviderNames.Local
                : metadataProviderName.Trim().ToLowerInvariant();
            return $"{MetadataKeyPrefix}{provider}";
        }

        return lane.ToString();
    }

    public static bool IsMetadataKey(string key) =>
        key.Equals(nameof(BackgroundTaskLane.Metadata), StringComparison.Ordinal)
        || key.StartsWith(MetadataKeyPrefix, StringComparison.Ordinal);

    public static int CountMetadataActive(ConcurrentDictionary<string, int> activeCountByKey)
    {
        var total = 0;
        foreach (var (key, count) in activeCountByKey)
        {
            if (IsMetadataKey(key))
                total += count;
        }

        return total;
    }

    /// <summary>
    /// Limit applied to a concrete gate key. Metadata provider keys are always
    /// <see cref="BackgroundTaskScheduling.MetadataProviderLimit"/>; other lanes use the configured
    /// lane limit.
    /// </summary>
    public static int ResolveKeyLimit(string key, Dictionary<BackgroundTaskLane, int> laneLimits)
    {
        if (key.StartsWith(MetadataKeyPrefix, StringComparison.Ordinal))
            return BackgroundTaskScheduling.MetadataProviderLimit;

        if (key.StartsWith($"{BackgroundTaskLane.Federation}:", StringComparison.Ordinal))
            return laneLimits.GetValueOrDefault(
                BackgroundTaskLane.Federation,
                BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.Federation));

        return Enum.TryParse<BackgroundTaskLane>(key, out var lane)
            ? laneLimits.GetValueOrDefault(lane, BackgroundTaskScheduling.GetDefaultLimit(lane))
            : BackgroundTaskScheduling.DefaultLaneLimit;
    }

    public static bool TryAcquire(
        ConcurrentDictionary<string, int> activeCountByKey,
        string key,
        int limit,
        Dictionary<BackgroundTaskLane, int>? laneLimits = null)
    {
        if (limit <= 0)
            return false;

        if (IsMetadataKey(key) && laneLimits is not null)
        {
            var metadataLimit = laneLimits.GetValueOrDefault(
                BackgroundTaskLane.Metadata,
                BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.Metadata));
            if (metadataLimit <= 0)
                return false;

            if (CountMetadataActive(activeCountByKey) >= metadataLimit)
                return false;
        }

        while (true)
        {
            var current = activeCountByKey.GetOrAdd(key, 0);
            if (current >= limit)
                return false;

            if (IsMetadataKey(key) && laneLimits is not null)
            {
                var metadataLimit = laneLimits.GetValueOrDefault(
                    BackgroundTaskLane.Metadata,
                    BackgroundTaskScheduling.GetDefaultLimit(BackgroundTaskLane.Metadata));
                if (CountMetadataActive(activeCountByKey) >= metadataLimit)
                    return false;
            }

            if (activeCountByKey.TryUpdate(key, current + 1, current))
                return true;
        }
    }

    public static void Release(ConcurrentDictionary<string, int> activeCountByKey, string key)
        => activeCountByKey.AddOrUpdate(key, 0, (_, count) => Math.Max(0, count - 1));
}
