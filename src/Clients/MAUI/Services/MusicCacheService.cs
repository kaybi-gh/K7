using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Services;

public class MusicCacheService : IMusicCacheService
{
    private const double LookaheadRemainingSeconds = 45;
    private const double LookaheadProgressThreshold = 0.8;

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

        var isTv = DeviceInfo.Current.Idiom == DeviceIdiom.TV;
        if (isTv)
        {
            LookaheadCount = 0;
            MaxCacheSizeBytes = 0;
        }

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
                    _audioPlayerService.CurrentTimeChanged += OnCurrentTimeChanged;
                    _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
                    _subscribed = true;
                    SyncCachePauseFromPlaybackState(_audioPlayerService.PlaybackState);
                }
            }

            return _audioPlayerService;
        }
    }

    private bool _initialLookaheadDone;
    private int _lookaheadGeneration;
    private bool _lookaheadStartedForCurrentTrack;

    public async Task<string?> GetCachedTrackPathAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        _ = AudioPlayerService; // Ensure event subscription is active

        if (!_initialLookaheadDone)
        {
            _initialLookaheadDone = true;
            // Do not prefetch at cold start while a stream may be opening.
        }

        var item = await _offlineStore.GetByIndexedFileIdAsync(indexedFileId, cancellationToken);
        return item is { IsCacheItem: true } && File.Exists(item.MediaLocalPath) ? item.MediaLocalPath : null;
    }

    public async Task InvalidateCacheAsync(CancellationToken cancellationToken = default)
    {
        await _offlineStore.RemoveAllCacheItemsAsync(cancellationToken);
    }

    private void OnQueueChanged()
    {
        _lookaheadStartedForCurrentTrack = false;
        ConsiderLateLookahead();
    }

    private void OnCurrentTrackChanged(AudioQueueItem? _)
    {
        _lookaheadStartedForCurrentTrack = false;
        SyncCachePauseFromPlaybackState(AudioPlayerService.PlaybackState);
    }

    private void OnCurrentTimeChanged(double _) => ConsiderLateLookahead();

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        SyncCachePauseFromPlaybackState(state);
        if (state is PlaybackState.Paused or PlaybackState.Idle or PlaybackState.Ended)
            ConsiderLateLookahead();
    }

    private void SyncCachePauseFromPlaybackState(PlaybackState state)
    {
        // Pause background cache downloads while actively streaming to avoid competing with playback I/O.
        var pause = state is PlaybackState.Playing or PlaybackState.Buffering;
        _downloadManager.SetMusicCacheDownloadsPaused(pause);
    }

    private void ConsiderLateLookahead()
    {
        if (_lookaheadStartedForCurrentTrack)
            return;

        var audio = AudioPlayerService;
        var duration = audio.Duration;
        var currentTime = audio.CurrentTime;
        if (duration <= 0 || currentTime <= 0)
            return;

        var remaining = duration - currentTime;
        var progress = currentTime / duration;
        if (remaining > LookaheadRemainingSeconds && progress < LookaheadProgressThreshold)
            return;

        _lookaheadStartedForCurrentTrack = true;
        ScheduleCacheLookaheadAsync().FireAndForget(_logger);
    }

    private async Task ScheduleCacheLookaheadAsync()
    {
        var generation = Interlocked.Increment(ref _lookaheadGeneration);
        try
        {
            // Brief settle after we enter the late-track window.
            await Task.Delay(TimeSpan.FromSeconds(2));
            if (generation != _lookaheadGeneration)
                return;

            await CacheLookaheadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music cache lookahead scheduling failed");
        }
    }

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

            var storageInfo = await _offlineStore.GetStorageInfoAsync();
            if (storageInfo.CacheBytes >= MaxCacheSizeBytes)
            {
                _logger.LogDebug("Music cache at capacity ({CacheBytes}/{MaxBytes}), skipping lookahead", storageInfo.CacheBytes, MaxCacheSizeBytes);
                return;
            }

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
                    MediaType = MediaType.MusicTrack,
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

        var wifiLookahead = _deviceStorageService.Get(PreferenceKeys.CACHE_LOOKAHEAD_WIFI);
        return wifiLookahead > 0 ? wifiLookahead : LookaheadCount;
    }
}
