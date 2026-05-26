using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Clients.Web.Services;

public class NoOpDownloadManager : IDownloadManager
{
#pragma warning disable CS0067
    public event Action<DownloadProgressInfo>? ProgressChanged;
    public event Action<DownloadCompletedInfo>? DownloadCompleted;
    public event Action<DownloadFailedInfo>? DownloadFailed;
#pragma warning restore CS0067

    public IReadOnlyList<DownloadQueueItem> Queue => [];

    public Task EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelAsync(Guid downloadId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CancelAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class NoOpOfflineMediaStore : IOfflineMediaStore
{
    public Task<bool> IsAvailableOfflineAsync(Guid indexedFileId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<DownloadedMediaItem?> GetByIndexedFileIdAsync(Guid indexedFileId, CancellationToken cancellationToken = default) => Task.FromResult<DownloadedMediaItem?>(null);
    public Task<IReadOnlyList<DownloadedMediaItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DownloadedMediaItem>>([]);
    public Task<IReadOnlyList<DownloadedMediaItem>> GetByMediaTypeAsync(MediaType mediaType, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DownloadedMediaItem>>([]);
    public Task<OfflineStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default) => Task.FromResult(new OfflineStorageInfo());
    public Task AddAsync(DownloadedMediaItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(Guid indexedFileId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAllCacheItemsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateLastPlaybackPositionAsync(Guid mediaId, double position, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<double> GetLastPlaybackPositionAsync(Guid mediaId, CancellationToken cancellationToken = default) => Task.FromResult(0.0);
}

public class NoOpConnectivityService : IConnectivityService
{
    public bool IsOnline => true;
    public bool IsWifi => true;
    public bool IsCellular => false;
#pragma warning disable CS0067
    public event Action<bool>? ConnectivityChanged;
#pragma warning restore CS0067
}

public class NoOpPlaybackJournal : IPlaybackJournal
{
    public Task RecordProgressAsync(Guid mediaId, Guid indexedFileId, double position, double duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordCompletedAsync(Guid mediaId, Guid indexedFileId, double duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordSkippedAsync(Guid mediaId, Guid indexedFileId, double position, double duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RecordRatingAsync(Guid mediaId, int value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<PendingPlaybackEvent>> GetPendingEventsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PendingPlaybackEvent>>([]);
    public Task MarkSyncedAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class NoOpMusicCacheService : IMusicCacheService
{
    public int LookaheadCount { get; set; }
    public long MaxCacheSizeBytes { get; set; }

    public Task<string?> GetCachedTrackPathAsync(Guid indexedFileId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    public Task InvalidateCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class NoOpServerConnectionService : IServerConnectionService
{
    public void DisconnectAndReset() { }
}
