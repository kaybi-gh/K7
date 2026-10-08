namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Sparse piece cache of an origin federated direct-stream for Peer local ffmpeg.
/// </summary>
public interface IFederatedMediaCache
{
    Task<FederatedMediaCacheEntry> EnsureOpenedAsync(
        Guid remoteIndexedFileId,
        Guid peerServerId,
        Guid remoteSessionId,
        string originDirectStreamUrl,
        CancellationToken cancellationToken = default);

    Task EnsureHeaderAndCuesAsync(
        FederatedMediaCacheEntry entry,
        CancellationToken cancellationToken = default);

    Task EnsureRangeAsync(
        FederatedMediaCacheEntry entry,
        long offset,
        long count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures pieces covering an approximate media time window (proportional byte map + margin).
    /// <paramref name="runwayBytes"/> caps the blocking fetch (0 = default). The coverage
    /// pump keeps filling ahead after ffmpeg starts.
    /// </summary>
    Task EnsureTimeWindowAsync(
        FederatedMediaCacheEntry entry,
        TimeSpan windowStart,
        TimeSpan windowEnd,
        TimeSpan? mediaDuration,
        CancellationToken cancellationToken = default,
        long runwayBytes = 0);

    bool TryGet(Guid remoteIndexedFileId, out FederatedMediaCacheEntry? entry);
}

public sealed class FederatedMediaCacheEntry
{
    public required Guid RemoteIndexedFileId { get; init; }
    public required Guid PeerServerId { get; set; }
    public required Guid RemoteSessionId { get; set; }
    public required string OriginDirectStreamUrl { get; set; }
    public required string LocalPath { get; init; }
    public required long ContentLength { get; init; }
    public DateTime LastAccessUtc { get; set; } = DateTime.UtcNow;
}
