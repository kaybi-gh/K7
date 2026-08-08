using System.Collections.Concurrent;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

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
    public string? DeviceClient { get; set; }
    public string? DeviceType { get; set; }
    public string? ThumbnailUrl { get; set; }
    public StreamDecisionDto? StreamDecision { get; set; }
    public DateTime StartedAt { get; init; }
    public double Position { get; set; }
    public double Duration { get; set; }
    public int State { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public string? SharedProfileName { get; set; }
    /// <summary>
    /// True when an OpenSubsonic client has sent reportPlayback with a real timeline.
    /// Admin UI shows a progress bar only when this is set for External devices.
    /// </summary>
    public bool HasPlaybackProgress { get; set; }
    public double PlaybackRate { get; set; } = 1.0;
}

public interface IActiveStreamTracker
{
    void Upsert(Guid sessionId, ActiveStreamInfo info);
    void UpdateStreamDecision(Guid sessionId, StreamDecisionDto decision);
    void Touch(Guid sessionId);
    void Remove(Guid sessionId);
    ActiveStreamInfo? GetStreamInfo(Guid sessionId);
    IReadOnlyList<ActiveStreamInfo> GetActiveStreams();

    /// <summary>OpenSubsonic HTTP body transfer for a media id (prefetch or real play).</summary>
    void BeginOpenSubsonicTransfer(Guid sessionId, Guid mediaId);
    /// <summary>
    /// Ends a transfer. When the now-playing media has no remaining transfers and a pending
    /// prefetch candidate exists, promotes that candidate to now-playing.
    /// </summary>
    void EndOpenSubsonicTransfer(Guid sessionId, Guid mediaId);
    bool IsOpenSubsonicTransferActive(Guid sessionId, Guid mediaId);
    void SetOpenSubsonicPending(Guid sessionId, ActiveStreamInfo pendingInfo);
}

public class ActiveStreamTracker : IActiveStreamTracker
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(90);
    private readonly ConcurrentDictionary<Guid, ActiveStreamInfo> _streams = new();
    private readonly ConcurrentDictionary<Guid, StreamDecisionDto> _pendingDecisions = new();
    private readonly ConcurrentDictionary<Guid, ActiveStreamInfo> _openSubsonicPending = new();
    private readonly ConcurrentDictionary<(Guid SessionId, Guid MediaId), int> _openSubsonicTransfers = new();

    public void Upsert(Guid sessionId, ActiveStreamInfo info)
    {
        info.LastUpdatedAt = DateTime.UtcNow;

        if (_streams.TryGetValue(sessionId, out var existing))
        {
            info.DeviceType ??= existing.DeviceType;

            if (existing.MediaId == info.MediaId)
            {
                info.ThumbnailUrl ??= existing.ThumbnailUrl;
                info.StreamDecision ??= existing.StreamDecision;
                info.HasPlaybackProgress = info.HasPlaybackProgress || existing.HasPlaybackProgress;
                if (info.PlaybackRate <= 0)
                    info.PlaybackRate = existing.PlaybackRate > 0 ? existing.PlaybackRate : 1.0;
            }
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
                    _openSubsonicPending.TryRemove(key, out _);
                }
            }
        }

        // Apply pending decision that arrived before the first Upsert
        if (info.StreamDecision is null && _pendingDecisions.TryRemove(sessionId, out var pending))
        {
            info.StreamDecision = pending;
        }

        _streams[sessionId] = info;
        _openSubsonicPending.TryRemove(sessionId, out _);
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

    public void Touch(Guid sessionId)
    {
        if (_streams.TryGetValue(sessionId, out var info))
            info.LastUpdatedAt = DateTime.UtcNow;
    }

    public void Remove(Guid sessionId)
    {
        _streams.TryRemove(sessionId, out _);
        _pendingDecisions.TryRemove(sessionId, out _);
        _openSubsonicPending.TryRemove(sessionId, out _);

        foreach (var key in _openSubsonicTransfers.Keys.Where(k => k.SessionId == sessionId).ToList())
            _openSubsonicTransfers.TryRemove(key, out _);
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
        // OpenSubsonic / external clients: keep Playing sessions alive. When reportPlayback
        // has provided a timeline, advance Position from PlaybackRate between polls.
        var now = DateTime.UtcNow;
        foreach (var info in _streams.Values)
        {
            if (info.State != (int)PlaybackState.Playing)
                continue;

            if (!string.Equals(info.DeviceClient, nameof(ClientType.External), StringComparison.OrdinalIgnoreCase))
                continue;

            if (info.HasPlaybackProgress)
            {
                var rate = info.PlaybackRate > 0 ? info.PlaybackRate : 1.0;
                var elapsed = (now - info.LastUpdatedAt).TotalSeconds * rate;
                if (elapsed > 0)
                {
                    var next = info.Position + elapsed;
                    info.Position = info.Duration > 0 ? Math.Min(info.Duration, next) : next;
                }
            }

            info.LastUpdatedAt = now;
        }

        var cutoff = now - StaleThreshold;

        var staleKeys = _streams
            .Where(kv => kv.Value.LastUpdatedAt < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in staleKeys)
            Remove(key);

        return _streams.Values.ToList();
    }

    public void BeginOpenSubsonicTransfer(Guid sessionId, Guid mediaId)
    {
        _openSubsonicTransfers.AddOrUpdate((sessionId, mediaId), 1, (_, count) => count + 1);
    }

    public void EndOpenSubsonicTransfer(Guid sessionId, Guid mediaId)
    {
        var key = (sessionId, mediaId);
        _openSubsonicTransfers.AddOrUpdate(key, 0, (_, count) => Math.Max(0, count - 1));
        if (_openSubsonicTransfers.TryGetValue(key, out var remaining) && remaining == 0)
            _openSubsonicTransfers.TryRemove(key, out _);

        if (!_streams.TryGetValue(sessionId, out var current))
            return;

        if (current.MediaId != mediaId)
            return;

        if (IsOpenSubsonicTransferActive(sessionId, mediaId))
            return;

        if (!_openSubsonicPending.TryRemove(sessionId, out var pending))
            return;

        // Current track transfer ended (skip / natural end) and a prefetched candidate is waiting.
        pending.LastUpdatedAt = DateTime.UtcNow;
        _streams[sessionId] = pending;
    }

    public bool IsOpenSubsonicTransferActive(Guid sessionId, Guid mediaId) =>
        _openSubsonicTransfers.TryGetValue((sessionId, mediaId), out var count) && count > 0;

    public void SetOpenSubsonicPending(Guid sessionId, ActiveStreamInfo pendingInfo)
    {
        pendingInfo.LastUpdatedAt = DateTime.UtcNow;
        _openSubsonicPending[sessionId] = pendingInfo;
    }
}
