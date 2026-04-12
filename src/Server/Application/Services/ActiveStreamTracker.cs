using System.Collections.Concurrent;
using K7.Shared.Dtos;

namespace K7.Server.Application.Services;

public sealed record ActiveStreamInfo
{
    public required Guid SessionId { get; init; }
    public required string IdentityUserId { get; init; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public Guid? MediaId { get; set; }
    public string? MediaTitle { get; set; }
    public string? MediaType { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public DateTime StartedAt { get; init; }
    public double Position { get; set; }
    public double Duration { get; set; }
    public int State { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

public interface IActiveStreamTracker
{
    void Upsert(Guid sessionId, ActiveStreamInfo info);
    void Remove(Guid sessionId);
    IReadOnlyList<ActiveStreamInfo> GetActiveStreams();
}

public class ActiveStreamTracker : IActiveStreamTracker
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(90);
    private readonly ConcurrentDictionary<Guid, ActiveStreamInfo> _streams = new();

    public void Upsert(Guid sessionId, ActiveStreamInfo info)
    {
        info.LastUpdatedAt = DateTime.UtcNow;
        _streams[sessionId] = info;
    }

    public void Remove(Guid sessionId)
    {
        _streams.TryRemove(sessionId, out _);
    }

    public IReadOnlyList<ActiveStreamInfo> GetActiveStreams()
    {
        var cutoff = DateTime.UtcNow - StaleThreshold;

        var staleKeys = _streams
            .Where(kv => kv.Value.LastUpdatedAt < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _streams.TryRemove(key, out _);
        }

        return _streams.Values.ToList();
    }
}
