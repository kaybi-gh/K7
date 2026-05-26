namespace K7.Clients.Shared.Interfaces;

public interface IDownloadManager
{
    event Action<DownloadProgressInfo>? ProgressChanged;
    event Action<DownloadCompletedInfo>? DownloadCompleted;
    event Action<DownloadFailedInfo>? DownloadFailed;

    IReadOnlyList<DownloadQueueItem> Queue { get; }

    Task EnqueueAsync(DownloadRequest request, CancellationToken cancellationToken = default);
    Task CancelAsync(Guid downloadId, CancellationToken cancellationToken = default);
    Task CancelAllAsync(CancellationToken cancellationToken = default);
}

public record DownloadRequest
{
    public required Guid IndexedFileId { get; init; }
    public required Guid MediaId { get; init; }
    public required string Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumTitle { get; init; }
    public string? CoverUrl { get; init; }
    public Server.Domain.Enums.MediaType MediaType { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int[]? SubtitleTrackIndices { get; init; }
    public K7.Shared.Dtos.Entities.Metadatas.Files.Tracks.SubtitleFileTrackDto[]? SubtitleTracks { get; init; }
    public bool IsCacheItem { get; init; }
}

public record DownloadQueueItem
{
    public required Guid DownloadId { get; init; }
    public required DownloadRequest Request { get; init; }
    public DownloadItemStatus Status { get; set; }
    public double Progress { get; set; }
    public long? TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
}

public record DownloadProgressInfo(Guid DownloadId, double Progress, long DownloadedBytes, long? TotalBytes);
public record DownloadCompletedInfo(Guid DownloadId, DownloadRequest Request, string LocalPath);
public record DownloadFailedInfo(Guid DownloadId, DownloadRequest Request, string Reason);

public enum DownloadItemStatus
{
    Queued,
    Preparing,
    Downloading,
    Completed,
    Failed,
    Cancelled
}
