using System.Text.Json;
using K7.Clients.MAUI.Data;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.EntityFrameworkCore;

namespace K7.Clients.MAUI.Services;

public class OfflineMediaStore : IOfflineMediaStore
{
    private readonly IDbContextFactory<OfflineMediaDbContext> _dbContextFactory;

    public OfflineMediaStore(IDbContextFactory<OfflineMediaDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<bool> IsAvailableOfflineAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.DownloadedMedia.AnyAsync(d => d.IndexedFileId == indexedFileId, cancellationToken);
    }

    public async Task<DownloadedMediaItem?> GetByIndexedFileIdAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DownloadedMedia.FirstOrDefaultAsync(d => d.IndexedFileId == indexedFileId, cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<DownloadedMediaItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.DownloadedMedia
            .OrderByDescending(d => d.DownloadedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<DownloadedMediaItem>> GetByMediaTypeAsync(MediaType mediaType, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.DownloadedMedia
            .Where(d => d.MediaType == mediaType)
            .OrderByDescending(d => d.DownloadedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ToModel).ToList();
    }

    public async Task<OfflineStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.DownloadedMedia
            .Select(d => new { d.FileSize, d.IsCacheItem })
            .ToListAsync(cancellationToken);

        var (deviceTotalBytes, deviceAvailableBytes) = DeviceStorageCapacity.GetForAppData();

        return new OfflineStorageInfo
        {
            UsedBytes = items.Sum(x => x.FileSize),
            CacheBytes = items.Where(x => x.IsCacheItem).Sum(x => x.FileSize),
            TotalItems = items.Count,
            CacheItems = items.Count(x => x.IsCacheItem),
            DeviceTotalBytes = deviceTotalBytes,
            DeviceAvailableBytes = deviceAvailableBytes
        };
    }

    public async Task AddAsync(DownloadedMediaItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = new DownloadedMediaEntity
        {
            Id = item.Id,
            IndexedFileId = item.IndexedFileId,
            MediaId = item.MediaId,
            MediaType = item.MediaType,
            Title = item.Title,
            Artist = item.Artist,
            AlbumTitle = item.AlbumTitle,
            CoverLocalPath = item.CoverLocalPath,
            MediaLocalPath = item.MediaLocalPath,
            SubtitleLocalPathsJson = item.SubtitleTracks is { Length: > 0 }
                ? JsonSerializer.Serialize(item.SubtitleTracks)
                : null,
            FileSize = item.FileSize,
            DownloadedAt = item.DownloadedAt,
            LastPlayedAt = item.LastPlayedAt,
            IsCacheItem = item.IsCacheItem
        };

        db.DownloadedMedia.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid indexedFileId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DownloadedMedia.FirstOrDefaultAsync(d => d.IndexedFileId == indexedFileId, cancellationToken);
        if (entity is null) return;

        // Delete local files
        TryDeleteFile(entity.MediaLocalPath);
        if (entity.CoverLocalPath is not null) TryDeleteFile(entity.CoverLocalPath);

        db.DownloadedMedia.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAllCacheItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var cacheItems = await db.DownloadedMedia
            .Where(d => d.IsCacheItem)
            .ToListAsync(cancellationToken);

        foreach (var item in cacheItems)
        {
            TryDeleteFile(item.MediaLocalPath);
            if (item.CoverLocalPath is not null) TryDeleteFile(item.CoverLocalPath);
        }

        db.DownloadedMedia.RemoveRange(cacheItems);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static DownloadedMediaItem ToModel(DownloadedMediaEntity entity)
    {
        return new DownloadedMediaItem
        {
            Id = entity.Id,
            IndexedFileId = entity.IndexedFileId,
            MediaId = entity.MediaId,
            MediaType = entity.MediaType,
            Title = entity.Title,
            Artist = entity.Artist,
            AlbumTitle = entity.AlbumTitle,
            CoverLocalPath = entity.CoverLocalPath,
            MediaLocalPath = entity.MediaLocalPath,
            SubtitleTracks = entity.SubtitleLocalPathsJson is not null
                ? JsonSerializer.Deserialize<SubtitleFileTrackDto[]>(entity.SubtitleLocalPathsJson)
                : null,
            FileSize = entity.FileSize,
            DownloadedAt = entity.DownloadedAt,
            LastPlayedAt = entity.LastPlayedAt,
            LastPlaybackPosition = entity.LastPlaybackPosition,
            IsCacheItem = entity.IsCacheItem
        };
    }

    public async Task UpdateLastPlaybackPositionAsync(Guid mediaId, double position, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DownloadedMedia.FirstOrDefaultAsync(d => d.MediaId == mediaId, cancellationToken);
        if (entity is null) return;

        entity.LastPlaybackPosition = position;
        entity.LastPlayedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<double> GetLastPlaybackPositionAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.DownloadedMedia.FirstOrDefaultAsync(d => d.MediaId == mediaId, cancellationToken);
        return entity?.LastPlaybackPosition ?? 0;
    }
}
