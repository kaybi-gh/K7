using System.Collections.Concurrent;
using K7.Clients.MAUI.Constants;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using PreferenceKeys = K7.Shared.PreferenceKeys;

namespace K7.Clients.MAUI.Services;

public class DownloadManager : IDownloadManager
{
    private readonly IDownloadService _downloadService;
    private readonly IK7ServerService _serverService;
    private readonly IOfflineMediaStore _offlineStore;
    private readonly IConnectivityService _connectivity;
    private readonly IDeviceStorageService _deviceStorageService;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DownloadManager> _logger;

    private readonly List<DownloadQueueItem> _queue = [];
    private readonly SemaphoreSlim _audioSemaphore = new(2, 2);
    private readonly SemaphoreSlim _videoSemaphore = new(1, 1);
    private CancellationTokenSource _globalCts = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _downloadCts = new();

    public event Action<DownloadProgressInfo>? ProgressChanged;
    public event Action<DownloadCompletedInfo>? DownloadCompleted;
    public event Action<DownloadFailedInfo>? DownloadFailed;

    public IReadOnlyList<DownloadQueueItem> Queue => _queue.AsReadOnly();

    public DownloadManager(
        IDownloadService downloadService,
        IK7ServerService serverService,
        IOfflineMediaStore offlineStore,
        IConnectivityService connectivity,
        IDeviceStorageService deviceStorageService,
        IDeviceService deviceService,
        ILogger<DownloadManager> logger)
    {
        _downloadService = downloadService;
        _serverService = serverService;
        _offlineStore = offlineStore;
        _connectivity = connectivity;
        _deviceStorageService = deviceStorageService;
        _deviceService = deviceService;
        _logger = logger;
    }

    public async Task EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        // Check network availability
        if (!IsDownloadAllowedOnCurrentNetwork())
        {
            _logger.LogInformation("Download blocked: network type not allowed (wifi={IsWifi}, cellular={IsCellular})", _connectivity.IsWifi, _connectivity.IsCellular);
            return;
        }

        // Check storage limits
        if (!await IsStorageAvailableAsync(request.IsCacheItem, cancellationToken))
        {
            _logger.LogInformation("Download blocked: storage limit reached (isCacheItem={IsCacheItem})", request.IsCacheItem);
            return;
        }

        // Check if already downloaded
        if (await _offlineStore.IsAvailableOfflineAsync(request.IndexedFileId, cancellationToken))
        {
            _logger.LogInformation("File {IndexedFileId} already available offline, skipping", request.IndexedFileId);
            return;
        }

        // Check if already in queue
        if (_queue.Any(q => q.Request.IndexedFileId == request.IndexedFileId && q.Status is DownloadItemStatus.Queued or DownloadItemStatus.Preparing or DownloadItemStatus.Downloading))
        {
            return;
        }

        var item = new DownloadQueueItem
        {
            DownloadId = Guid.NewGuid(),
            Request = request,
            Status = DownloadItemStatus.Queued,
            Progress = 0,
            TotalBytes = null,
            DownloadedBytes = 0
        };

        _queue.Add(item);

        _ = Task.Run(() => ProcessDownloadAsync(item), _globalCts.Token);
    }

    public Task CancelAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        if (_downloadCts.TryGetValue(downloadId, out var cts))
        {
            cts.Cancel();
            _downloadCts.TryRemove(downloadId, out _);
        }

        var item = _queue.FirstOrDefault(q => q.DownloadId == downloadId);
        if (item is not null)
        {
            _queue.Remove(item);
        }

        return Task.CompletedTask;
    }

    public Task CancelAllAsync(CancellationToken cancellationToken = default)
    {
        _globalCts.Cancel();
        _globalCts.Dispose();
        _globalCts = new CancellationTokenSource();
        foreach (var cts in _downloadCts.Values)
        {
            cts.Cancel();
        }
        _downloadCts.Clear();
        _queue.Clear();
        return Task.CompletedTask;
    }

    private async Task ProcessDownloadAsync(DownloadQueueItem item)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);
        _downloadCts[item.DownloadId] = cts;
        var cancellationToken = cts.Token;

        var isVideo = item.Request.MediaType is MediaType.Movie or MediaType.SerieEpisode;
        var semaphore = isVideo ? _videoSemaphore : _audioSemaphore;

        try
        {
            _logger.LogInformation("Download {DownloadId} waiting for semaphore (video={IsVideo})", item.DownloadId, isVideo);
            await semaphore.WaitAsync(cancellationToken);

            // Step 1: Prepare download on server
            UpdateStatus(item, DownloadItemStatus.Preparing);
            _logger.LogInformation("Download {DownloadId} preparing on server for IndexedFile {IndexedFileId}", item.DownloadId, item.Request.IndexedFileId);

            var deviceId = GetDeviceId();
            var downloadDto = await _downloadService.PrepareDownloadAsync(new K7.Shared.Dtos.Requests.PrepareDownloadRequest
            {
                IndexedFileId = item.Request.IndexedFileId,
                DeviceId = deviceId,
                AudioTrackIndex = item.Request.AudioTrackIndex,
                SubtitleTrackIndices = item.Request.SubtitleTrackIndices
            }, cancellationToken);

            _logger.LogInformation("Download {DownloadId} server returned status {Status}, downloadId={ServerDownloadId}", item.DownloadId, downloadDto.Status, downloadDto.Id);

            // Step 2: Wait for ready (poll if transcoding)
            while (downloadDto.Status is DownloadStatus.Pending or DownloadStatus.Transcoding)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(MauiTimeouts.DownloadRetryDelay, cancellationToken);
                downloadDto = await _downloadService.GetDownloadAsync(downloadDto.Id, cancellationToken);
                _logger.LogDebug("Download {DownloadId} poll: status={Status}", item.DownloadId, downloadDto.Status);
            }

            if (downloadDto.Status == DownloadStatus.Failed)
            {
                throw new InvalidOperationException($"Server preparation failed: {downloadDto.FailureReason}");
            }

            // Step 3: Download file
            UpdateStatus(item, DownloadItemStatus.Downloading);

            var localPath = GetLocalFilePath(item.Request);
            var directory = Path.GetDirectoryName(localPath)!;
            Directory.CreateDirectory(directory);

            var fileUrl = _downloadService.GetDownloadFileUrl(downloadDto.Id);
            var absoluteUrl = _serverService.GetAbsoluteUri(fileUrl);

            _logger.LogInformation("Download {DownloadId} starting file transfer from {Url} to {LocalPath}, size={FileSize}", item.DownloadId, absoluteUrl, localPath, downloadDto.FileSize);

            await DownloadFileAsync(absoluteUrl!.ToString(), localPath, item, downloadDto.FileSize, cancellationToken);

            // Download cover image if available
            string? coverLocalPath = null;
            if (!string.IsNullOrEmpty(item.Request.CoverUrl))
            {
                try
                {
                    coverLocalPath = await DownloadCoverAsync(item.Request.CoverUrl, item.Request.IndexedFileId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download cover for {DownloadId}", item.DownloadId);
                }
            }

            // Step 4: Register in offline store
            await _offlineStore.AddAsync(new DownloadedMediaItem
            {
                Id = Guid.NewGuid(),
                IndexedFileId = item.Request.IndexedFileId,
                MediaId = item.Request.MediaId,
                MediaType = item.Request.MediaType,
                Title = item.Request.Title,
                Artist = item.Request.Artist,
                AlbumTitle = item.Request.AlbumTitle,
                CoverLocalPath = coverLocalPath,
                MediaLocalPath = localPath,
                SubtitleTracks = item.Request.SubtitleTracks,
                FileSize = downloadDto.FileSize ?? new FileInfo(localPath).Length,
                DownloadedAt = DateTimeOffset.UtcNow,
                IsCacheItem = item.Request.IsCacheItem
            }, cancellationToken);

            UpdateStatus(item, DownloadItemStatus.Completed);
            DownloadCompleted?.Invoke(new DownloadCompletedInfo(item.DownloadId, item.Request, localPath));
        }
        catch (OperationCanceledException)
        {
            UpdateStatus(item, DownloadItemStatus.Cancelled);
            _logger.LogInformation("Download {DownloadId} cancelled", item.DownloadId);
        }
        catch (Exception ex)
        {
            UpdateStatus(item, DownloadItemStatus.Failed);
            _logger.LogError(ex, "Download {DownloadId} failed for IndexedFile {IndexedFileId}", item.DownloadId, item.Request.IndexedFileId);
            DownloadFailed?.Invoke(new DownloadFailedInfo(item.DownloadId, item.Request, ex.Message));
        }
        finally
        {
            semaphore.Release();
            _downloadCts.TryRemove(item.DownloadId, out _);
        }
    }

    private async Task DownloadFileAsync(string url, string localPath, DownloadQueueItem item, long? totalBytes, CancellationToken cancellationToken)
    {
        var httpClient = _serverService.HttpClient;

        using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength ?? totalBytes;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            var progress = contentLength > 0 ? (double)totalRead / contentLength.Value : 0;
            item.Progress = progress;
            item.DownloadedBytes = totalRead;
            item.TotalBytes = contentLength;
            ProgressChanged?.Invoke(new DownloadProgressInfo(item.DownloadId, progress, totalRead, contentLength));
        }
    }

    private static string GetLocalFilePath(DownloadRequest request)
    {
        var basePath = Path.Combine(FileSystem.AppDataDirectory, "downloads");
        var subFolder = request.MediaType switch
        {
            MediaType.MusicTrack => "music",
            MediaType.Movie => "movies",
            MediaType.SerieEpisode => "episodes",
            _ => "other"
        };

        var safeFileName = SanitizeFileName(request.Title);
        var extension = request.MediaType == MediaType.MusicTrack ? ".m4a" : ".mp4";

        return Path.Combine(basePath, subFolder, $"{request.IndexedFileId:N}_{safeFileName}{extension}");
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).Take(50).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "download" : sanitized;
    }

    private Guid GetDeviceId()
    {
        var stored = _deviceService.GetDeviceId();
        return Guid.TryParse(stored, out var id) ? id : throw new InvalidOperationException("Device not registered");
    }

    private async Task<string?> DownloadCoverAsync(string coverUrl, Guid indexedFileId, CancellationToken cancellationToken)
    {
        var coversDir = Path.Combine(FileSystem.AppDataDirectory, "downloads", "covers");
        Directory.CreateDirectory(coversDir);

        var coverPath = Path.Combine(coversDir, $"{indexedFileId:N}.jpg");
        if (File.Exists(coverPath) && new FileInfo(coverPath).Length > 100)
            return coverPath;

        var absoluteUrl = _serverService.GetAbsoluteUri(coverUrl) ?? new Uri(coverUrl);

        _logger.LogDebug("Downloading cover from {Url} to {Path}", absoluteUrl, coverPath);

        using var response = await _serverService.HttpClient.GetAsync(absoluteUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(coverPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(fileStream, cancellationToken);

        if (new FileInfo(coverPath).Length < 100)
        {
            _logger.LogWarning("Cover download resulted in an empty or corrupt file: {Path}", coverPath);
            File.Delete(coverPath);
            return null;
        }

        return coverPath;
    }

    private bool IsDownloadAllowedOnCurrentNetwork()
    {
        if (!_connectivity.IsOnline) return false;

        if (_connectivity.IsWifi)
        {
            var allowWifi = _deviceStorageService.Get(PreferenceKeys.DOWNLOAD_ALLOW_WIFI, true);
            return allowWifi;
        }

        if (_connectivity.IsCellular)
        {
            var allowMobile = _deviceStorageService.Get(PreferenceKeys.DOWNLOAD_ALLOW_MOBILE_DATA);
            return allowMobile;
        }

        // Unknown network type (ethernet, etc.) - allow
        return true;
    }

    private async Task<bool> IsStorageAvailableAsync(bool isCacheItem, CancellationToken cancellationToken)
    {
        var storageInfo = await _offlineStore.GetStorageInfoAsync(cancellationToken);

        if (isCacheItem)
        {
            var maxCache = _deviceStorageService.Get(PreferenceKeys.MAX_CACHE_STORAGE_BYTES);
            if (maxCache <= 0) maxCache = 500L * 1024 * 1024;
            return storageInfo.CacheBytes < maxCache;
        }

        var maxDownload = _deviceStorageService.Get(PreferenceKeys.MAX_DOWNLOAD_STORAGE_BYTES);
        if (maxDownload <= 0) maxDownload = 2L * 1024 * 1024 * 1024;
        return storageInfo.UsedBytes - storageInfo.CacheBytes < maxDownload;
    }

    private void UpdateStatus(DownloadQueueItem item, DownloadItemStatus status)
    {
        item.Status = status;
        ProgressChanged?.Invoke(new DownloadProgressInfo(item.DownloadId, item.Progress, item.DownloadedBytes, item.TotalBytes));
    }
}
