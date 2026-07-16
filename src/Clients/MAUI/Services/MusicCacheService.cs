using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Services;

public class MusicCacheService : IMusicCacheService
{
    private readonly IDownloadManager _downloadManager;
    private readonly IOfflineMediaStore _offlineStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<MusicCacheService> _logger;

    private IAudioPlayerService? _audioPlayerService;
    private bool _subscribed;

    public int LookaheadCount { get; set; } = 3;
    public long MaxCacheSizeBytes { get; set; } = 500 * 1024 * 1024; // 500 MB default

    public MusicCacheService(
        IDownloadManager downloadManager,
        IOfflineMediaStore offlineStore,
        IServiceProvider serviceProvider,
        IDeviceStorageService deviceStorageService,
        IConnectivityService connectivity,
        ILogger<MusicCacheService> logger)
    {
        _downloadManager = downloadManager;
        _offlineStore = offlineStore;
        _serviceProvider = serviceProvider;
        _deviceStorageService = deviceStorageService;
        _connectivity = connectivity;
        _logger = logger;

        var storedMax = _deviceStorageService.Get(PreferenceKeys.MAX_CACHE_STORAGE_BYTES);
        if (storedMax > 0)
            MaxCacheSizeBytes = storedMax;

        var storedLookahead = _deviceStorageService.Get(PreferenceKeys.CACHE_LOOKAHEAD_WIFI);
        if (storedLookahead > 0)
            LookaheadCount = storedLookahead;
    }

    private IAudioPlayerService AudioPlayerService
    {
        get
        {
            if (_audioPlayerService is null)
            {
                _audioPlayerService = _serviceProvider.GetRequiredService<IAudioPlayerService>();
                if (!_subscribed)
                {
                    _audioPlayerService.QueueChanged += OnQueueChanged;
                    _audioPlayerService.CurrentTrackChanged += OnCurrentTrackChanged;
                    _subscribed = true;
                }
            }

            return _audioPlayerService;
        }
    }

    private bool _initialLookaheadDone;

    public async Task<string?> GetCachedTrackPathAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        _ = AudioPlayerService; // Ensure event subscription is active

        if (!_initialLookaheadDone)
        {
            _initialLookaheadDone = true;
            _ = CacheLookaheadAsync();
        }

        var item = await _offlineStore.GetByIndexedFileIdAsync(indexedFileId, cancellationToken);
        return item is { IsCacheItem: true } && File.Exists(item.MediaLocalPath) ? item.MediaLocalPath : null;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        await _offlineStore.RemoveAllCacheItemsAsync(cancellationToken);
    }

    private void OnQueueChanged() => CacheLookaheadAsync().FireAndForget(_logger);

    private void OnCurrentTrackChanged(AudioQueueItem? _) => CacheLookaheadAsync().FireAndForget(_logger);

    private async Task CacheLookaheadAsync()
    {
        try
        {
            var effectiveLookahead = GetEffectiveLookaheadCount();
            if (effectiveLookahead <= 0)
            {
                _logger.LogDebug("Music cache lookahead disabled for current network");
                return;
            }

            var queue = AudioPlayerService.Queue;
            var currentIndex = AudioPlayerService.CurrentIndex;

            if (queue.Count == 0 || currentIndex < 0) return;

            // Check cache size limit
            var storageInfo = await _offlineStore.GetStorageInfoAsync();
            if (storageInfo.CacheBytes >= MaxCacheSizeBytes)
            {
                _logger.LogDebug("Music cache at capacity ({CacheBytes}/{MaxBytes}), skipping lookahead", storageInfo.CacheBytes, MaxCacheSizeBytes);
                return;
            }

            // Cache the next N tracks
            for (var i = 1; i <= effectiveLookahead && currentIndex + i < queue.Count; i++)
            {
                var nextItem = queue[currentIndex + i];
                var alreadyCached = await _offlineStore.IsAvailableOfflineAsync(nextItem.IndexedFileId);
                if (alreadyCached) continue;

                await _downloadManager.EnqueueAsync(new DownloadRequest
                {
                    IndexedFileId = nextItem.IndexedFileId,
                    MediaId = nextItem.MediaId,
                    Title = nextItem.Title,
                    Artist = nextItem.Artist,
                    AlbumTitle = nextItem.AlbumTitle,
                    CoverUrl = nextItem.CoverUrl,
                    MediaType = Server.Domain.Enums.MediaType.MusicTrack,
                    IsCacheItem = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music cache lookahead failed");
        }
    }

    private int GetEffectiveLookaheadCount()
    {
        if (_connectivity.IsCellular)
        {
            var mobileLookahead = _deviceStorageService.Get(PreferenceKeys.CACHE_LOOKAHEAD_MOBILE);
            return mobileLookahead;
        }

        // WiFi or other network type
        var wifiLookahead = _deviceStorageService.Get(PreferenceKeys.CACHE_LOOKAHEAD_WIFI);
        return wifiLookahead > 0 ? wifiLookahead : LookaheadCount;
    }
}
