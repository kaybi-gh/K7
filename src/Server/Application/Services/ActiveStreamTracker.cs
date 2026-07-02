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
    public string? DeviceType { get; set; }
    public string? ThumbnailUrl { get; set; }
    public StreamDecisionDto? StreamDecision { get; set; }
    public DateTime StartedAt { get; init; }
    public double Position { get; set; }
    public double Duration { get; set; }
    public int State { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string? SharedProfileName { get; set; }
}

public interface IActiveStreamTracker
{
    void Upsert(Guid sessionId, ActiveStreamInfo info);
    void UpdateStreamDecision(Guid sessionId, StreamDecisionDto decision);
    void Remove(Guid sessionId);
    ActiveStreamInfo? GetStreamInfo(Guid sessionId);
    IReadOnlyList<ActiveStreamInfo> GetActiveStreams();
}

public class ActiveStreamTracker : IActiveStreamTracker
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(90);
    private readonly ConcurrentDictionary<Guid, ActiveStreamInfo> _streams = new();
    private readonly ConcurrentDictionary<Guid, StreamDecisionDto> _pendingDecisions = new();

    public void Upsert(Guid sessionId, ActiveStreamInfo info)
    {
        info.LastUpdatedAt = DateTime.UtcNow;

        if (_streams.TryGetValue(sessionId, out var existing))
        {
            info.ThumbnailUrl ??= existing.ThumbnailUrl;
            info.StreamDecision ??= existing.StreamDecision;
            info.DeviceType ??= existing.DeviceType;
        }
        else
        {
            // Remove stale sessions from the same user + same media (user restarted playback)
            if (info.UserId.HasValue && info.MediaId.HasValue)
            {
                var staleKeys = _streams
                    .Where(kv => kv.Key != sessionId
                        && kv.Value.UserId == info.UserId
                        && kv.Value.MediaId == info.MediaId)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in staleKeys)
                {
                    _streams.TryRemove(key, out _);
                    _pendingDecisions.TryRemove(key, out _);
                }
            }
        }

        // Apply pending decision that arrived before the first Upsert
        if (info.StreamDecision is null && _pendingDecisions.TryRemove(sessionId, out var pending))
        {
            info.StreamDecision = pending;
        }

        _streams[sessionId] = info;
    }

    public void UpdateStreamDecision(Guid sessionId, StreamDecisionDto decision)
    {
        if (_streams.TryGetValue(sessionId, out var info))
        {
            info.StreamDecision = decision;
        }
        else
        {
            // Stream not yet tracked; store for when Upsert is called
            _pendingDecisions[sessionId] = decision;
        }
    }

    public void Remove(Guid sessionId)
    {
        _streams.TryRemove(sessionId, out _);
        _pendingDecisions.TryRemove(sessionId, out _);
    }

    public ActiveStreamInfo? GetStreamInfo(Guid sessionId)
    {
        if (_streams.TryGetValue(sessionId, out var info))
            return info;

        // Return a minimal info with pending decision if stream not yet fully tracked
        if (_pendingDecisions.TryGetValue(sessionId, out var pending))
            return new ActiveStreamInfo { SessionId = sessionId, IdentityUserId = "", StartedAt = default, StreamDecision = pending };

        return null;
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
