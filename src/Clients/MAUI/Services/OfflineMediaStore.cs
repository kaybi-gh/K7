using System.Text.Json;
using K7.Clients.MAUI.Data;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
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

        return new OfflineStorageInfo
        {
            UsedBytes = items.Sum(x => x.FileSize),
            CacheBytes = items.Where(x => x.IsCacheItem).Sum(x => x.FileSize),
            TotalItems = items.Count,
            CacheItems = items.Count(x => x.IsCacheItem)
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
            SubtitleLocalPathsJson = item.SubtitleLocalPaths is { Length: > 0 }
                ? JsonSerializer.Serialize(item.SubtitleLocalPaths)
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
        if (entity.SubtitleLocalPathsJson is not null)
        {
            var paths = JsonSerializer.Deserialize<string[]>(entity.SubtitleLocalPathsJson);
            if (paths is not null)
            {
                foreach (var path in paths) TryDeleteFile(path);
            }
        }

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
            SubtitleLocalPaths = entity.SubtitleLocalPathsJson is not null
                ? JsonSerializer.Deserialize<string[]>(entity.SubtitleLocalPathsJson)
                : null,
            FileSize = entity.FileSize,
            DownloadedAt = entity.DownloadedAt,
            LastPlayedAt = entity.LastPlayedAt,
            IsCacheItem = entity.IsCacheItem
        };
    }
}
