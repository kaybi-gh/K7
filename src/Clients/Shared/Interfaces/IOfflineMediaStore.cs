using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Clients.Shared.Interfaces;

public interface IOfflineMediaStore
{
    Task<bool> IsAvailableOfflineAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
    Task<DownloadedMediaItem?> GetByIndexedFileIdAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DownloadedMediaItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DownloadedMediaItem>> GetByMediaTypeAsync(MediaType mediaType, CancellationToken cancellationToken = default);
    Task<OfflineStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);
    Task AddAsync(DownloadedMediaItem item, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid indexedFileId, CancellationToken cancellationToken = default);
    Task RemoveAllCacheItemsAsync(CancellationToken cancellationToken = default);
    Task UpdateLastPlaybackPositionAsync(Guid mediaId, double position, CancellationToken cancellationToken = default);
    Task<double> GetLastPlaybackPositionAsync(Guid mediaId, CancellationToken cancellationToken = default);
}

public record DownloadedMediaItem
{
    public required Guid Id { get; init; }
    public required Guid IndexedFileId { get; init; }
    public required Guid MediaId { get; init; }
    public required MediaType MediaType { get; init; }
    public required string Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumTitle { get; init; }
    public string? CoverLocalPath { get; init; }
    public required string MediaLocalPath { get; init; }
    public SubtitleFileTrackDto[]? SubtitleTracks { get; init; }
    public required long FileSize { get; init; }
    public required DateTimeOffset DownloadedAt { get; init; }
    public DateTimeOffset? LastPlayedAt { get; set; }
    public double LastPlaybackPosition { get; set; }
    public required bool IsCacheItem { get; init; }
}

public record OfflineStorageInfo
{
    public long UsedBytes { get; init; }
    public long CacheBytes { get; init; }
    public int TotalItems { get; init; }
    public int CacheItems { get; init; }
}
