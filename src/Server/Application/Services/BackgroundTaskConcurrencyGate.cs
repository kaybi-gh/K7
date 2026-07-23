using System.Collections.Concurrent;

namespace K7.Server.Application.Services;

internal static class BackgroundTaskConcurrencyGate
{
    public static bool TryAcquire(ConcurrentDictionary<string, int> activeCountByGroup, string? group, int limit)
    {
        if (group is null)
            return true;

        if (limit <= 0)
            return false;

        while (true)
        {
            var current = activeCountByGroup.GetOrAdd(group, 0);
            if (current >= limit)
                return false;

            if (activeCountByGroup.TryUpdate(group, current + 1, current))
                return true;
        }
    }

    public static void Release(ConcurrentDictionary<string, int> activeCountByGroup, string? group)
    {
        if (group is null)
            return;

        activeCountByGroup.AddOrUpdate(group, 0, (_, count) => Math.Max(0, count - 1));
    }
}
