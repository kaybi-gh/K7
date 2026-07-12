using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceDownloadsPage : ComponentBase, IDisposable
{
    private int _activeCount;
    private List<DownloadGroup> _musicGroups = [];
    private List<DownloadedMediaItem> _videoItems = [];
    private List<DownloadedMediaItem> _cachedItems = [];
    private List<DownloadGroup> _cachedGroups = [];
    private OfflineStorageInfo? _storageInfo;
    private DownloadTab _activeTab = DownloadTab.All;
    private string _searchQuery = string.Empty;
    private IReadOnlyList<ButtonGroupOption<DownloadTab>> _filterOptions = [];
    private long _maxDownloadBytes;
    private long _maxCacheBytes;
    private bool _progressDirty;
    private System.Timers.Timer? _progressThrottle;

    private const long DefaultMaxDownloadBytes = 2L * 1024 * 1024 * 1024;
    private const long DefaultMaxCacheBytes = 500L * 1024 * 1024;

    private List<DownloadGroup> FilteredMusicGroups => string.IsNullOrWhiteSpace(_searchQuery)
        ? _musicGroups
        : _musicGroups.Where(g =>
            g.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
            (g.Subtitle?.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

    private List<DownloadedMediaItem> FilteredVideoItems => string.IsNullOrWhiteSpace(_searchQuery)
        ? _videoItems
        : _videoItems.Where(i => i.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

    private void SetTab(DownloadTab tab)
    {
        _activeTab = tab;
    }

    private void RebuildFilterOptions()
    {
        var options = new List<ButtonGroupOption<DownloadTab>>
        {
            new(DownloadTab.All, L["All"])
        };

        if (_musicGroups.Count > 0)
            options.Add(new(DownloadTab.Music, $"{L["Music"]} ({_musicGroups.Count})"));

        if (_videoItems.Count > 0)
            options.Add(new(DownloadTab.Videos, $"{L["Videos"]} ({_videoItems.Count})"));

        if (_cachedItems.Count > 0)
            options.Add(new(DownloadTab.Cache, $"{L["CacheUsed"]} ({_cachedItems.Count})"));

        _filterOptions = options;
    }

    protected override async Task OnInitializedAsync()
    {
        _maxDownloadBytes = DeviceStorageService.Get(PreferenceKeys.MAX_DOWNLOAD_STORAGE_BYTES);
        if (_maxDownloadBytes <= 0) _maxDownloadBytes = DefaultMaxDownloadBytes;

        _maxCacheBytes = DeviceStorageService.Get(PreferenceKeys.MAX_CACHE_STORAGE_BYTES);
        if (_maxCacheBytes <= 0) _maxCacheBytes = DefaultMaxCacheBytes;

        _progressThrottle = new System.Timers.Timer(500) { AutoReset = false };
        _progressThrottle.Elapsed += async (_, _) =>
        {
            if (!_progressDirty) return;
            _progressDirty = false;
            await InvokeAsync(StateHasChanged);
        };

        await LoadDataAsync();
        DownloadManager.ProgressChanged += OnProgressChanged;
        DownloadManager.DownloadCompleted += OnDownloadCompleted;
    }

    private async Task LoadDataAsync()
    {
        _activeCount = DownloadManager.Queue
            .Count(q => q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading);

        var allItems = await OfflineStore.GetAllAsync();
        var musicItems = allItems.Where(i => i.MediaType == MediaType.MusicTrack && !i.IsCacheItem).ToList();
        _videoItems = allItems.Where(i => i.MediaType is MediaType.Movie or MediaType.SerieEpisode && !i.IsCacheItem).ToList();
        _cachedItems = allItems.Where(i => i.IsCacheItem).ToList();
        _storageInfo = await OfflineStore.GetStorageInfoAsync();

        _musicGroups = musicItems
            .GroupBy(i => i.AlbumTitle ?? i.Title)
            .Select(g => new DownloadGroup
            {
                Title = g.Key,
                Subtitle = g.First().Artist,
                CoverUrl = g.First().CoverLocalPath,
                TotalSize = g.Sum(x => x.FileSize),
                Items = g.ToList()
            })
            .ToList();

        _cachedGroups = _cachedItems
            .GroupBy(i => i.AlbumTitle ?? i.Title)
            .Select(g => new DownloadGroup
            {
                Title = g.Key,
                Subtitle = g.First().Artist,
                CoverUrl = g.First().CoverLocalPath,
                TotalSize = g.Sum(x => x.FileSize),
                Items = g.ToList()
            })
            .ToList();

        RebuildFilterOptions();
    }

    private void NavigateToQueue()
    {
        Navigation.NavigateTo("/my-space/downloads/queue");
    }

    private async Task RemoveAsync(Guid indexedFileId)
    {
        await OfflineStore.RemoveAsync(indexedFileId);
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task RemoveGroupAsync(DownloadGroup group)
    {
        foreach (var item in group.Items)
        {
            await OfflineStore.RemoveAsync(item.IndexedFileId);
        }
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task PlayGroupAsync(DownloadGroup group)
    {
        if (group.Items.Count == 0)
            return;

        var tracks = group.Items.Select(item => new AudioQueueItem
        {
            IndexedFileId = item.IndexedFileId,
            MediaId = item.MediaId,
            Title = item.Title,
            Artist = item.Artist,
            AlbumTitle = item.AlbumTitle,
            CoverUrl = DeviceService.GetLocalFileUrl(item.CoverLocalPath),
            LocalPath = item.MediaLocalPath
        }).ToList();

        await AudioPlayerService.PlayTracksAsync(tracks);
    }

    private async Task ShuffleAllMusicAsync()
    {
        var allTracks = _musicGroups
            .SelectMany(g => g.Items)
            .Select(item => new AudioQueueItem
            {
                IndexedFileId = item.IndexedFileId,
                MediaId = item.MediaId,
                Title = item.Title,
                Artist = item.Artist,
                AlbumTitle = item.AlbumTitle,
                CoverUrl = DeviceService.GetLocalFileUrl(item.CoverLocalPath),
                LocalPath = item.MediaLocalPath
            })
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        if (allTracks.Count == 0)
            return;

        await AudioPlayerService.PlayTracksAsync(allTracks);
    }

    private async Task PlayVideoAsync(DownloadedMediaItem item)
    {
        PlaybackProgressTracker.StartTracking(item.MediaId, isAuthenticated: true, indexedFileId: item.IndexedFileId);

        if (item.SubtitleTracks is { Length: > 0 })
        {
            PlayerService.SetSubtitleTracks(item.SubtitleTracks);
        }

        await PlayerService.ShowAsync();
        PlayerService.Source = new PlayerSource
        {
            MediaId = item.MediaId,
            Url = item.MediaLocalPath,
            MimeType = "video/mp4"
        };

        if (item.LastPlaybackPosition > 0)
        {
            PlayerService.Seek(item.LastPlaybackPosition);
        }
    }

    private void OnProgressChanged(DownloadProgressInfo info)
    {
        _activeCount = DownloadManager.Queue
            .Count(q => q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading);
        _progressDirty = true;
        _progressThrottle?.Start();
    }

    private async void OnDownloadCompleted(DownloadCompletedInfo info)
    {
        await InvokeAsync(async () =>
        {
            await LoadDataAsync();
            StateHasChanged();
        });
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private double GetDownloadStoragePercent()
    {
        if (_storageInfo is null || _maxDownloadBytes <= 0) return 0;
        return Math.Min(100, (double)(_storageInfo.UsedBytes - _storageInfo.CacheBytes) / _maxDownloadBytes * 100);
    }

    private double GetCacheStoragePercent()
    {
        if (_storageInfo is null || _maxCacheBytes <= 0) return 0;
        return Math.Min(100, (double)_storageInfo.CacheBytes / _maxCacheBytes * 100);
    }

    private void OnMaxDownloadChanged(long value)
    {
        _maxDownloadBytes = value;
        DeviceStorageService.Set(PreferenceKeys.MAX_DOWNLOAD_STORAGE_BYTES, value);
    }

    private void OnMaxCacheChanged(long value)
    {
        _maxCacheBytes = value;
        DeviceStorageService.Set(PreferenceKeys.MAX_CACHE_STORAGE_BYTES, value);
        MusicCacheService.MaxCacheSizeBytes = value;
    }

    public void Dispose()
    {
        _progressThrottle?.Dispose();
        DownloadManager.ProgressChanged -= OnProgressChanged;
        DownloadManager.DownloadCompleted -= OnDownloadCompleted;
    }

    private sealed class DownloadGroup
    {
        public string Title { get; init; } = string.Empty;
        public string? Subtitle { get; init; }
        public string? CoverUrl { get; init; }
        public long TotalSize { get; init; }
        public List<DownloadedMediaItem> Items { get; init; } = [];
    }

    private enum DownloadTab
    {
        All,
        Music,
        Videos,
        Cache
    }
}

