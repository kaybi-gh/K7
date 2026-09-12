using System.Collections.Concurrent;

namespace K7.Server.Application.Features.Scrobbling.Services;

public interface IScrobbleProgressThrottle
{
    bool TryAcquire(Guid sessionId, TimeSpan minInterval);
}

/// <summary>
/// Limits webhook progress scrobbles per playback session (in-memory, process-local).
/// </summary>
public sealed class ScrobbleProgressThrottle : IScrobbleProgressThrottle
{
    private readonly ConcurrentDictionary<Guid, long> _lastTicks = new();

    public bool TryAcquire(Guid sessionId, TimeSpan minInterval)
    {
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var min = minInterval.Ticks;

        while (true)
        {
            if (!_lastTicks.TryGetValue(sessionId, out var last))
            {
                if (_lastTicks.TryAdd(sessionId, now))
                    return true;
                continue;
            }

            if (now - last < min)
                return false;

            if (_lastTicks.TryUpdate(sessionId, now, last))
                return true;
        }
    }
}

public interface IScrobbleCompletionGate
{
    /// <summary>
    /// Returns true only for the first completion claim for this playback session.
    /// Prevents duplicate Watched marks when concurrent progress reports both raise completed.
    /// </summary>
    bool TryClaim(Guid sessionId);
}

public sealed class ScrobbleCompletionGate : IScrobbleCompletionGate
{
    private readonly ConcurrentDictionary<Guid, byte> _claimed = new();

    public bool TryClaim(Guid sessionId) => _claimed.TryAdd(sessionId, 0);
}
