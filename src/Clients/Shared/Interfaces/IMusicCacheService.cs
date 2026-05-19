namespace K7.Clients.Shared.Interfaces;

public interface IMusicCacheService
{
    int LookaheadCount { get; set; }
    long MaxCacheSizeBytes { get; set; }

    Task<string?> GetCachedTrackPathAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
    Task InvalidateCacheAsync(CancellationToken cancellationToken = default);
}
