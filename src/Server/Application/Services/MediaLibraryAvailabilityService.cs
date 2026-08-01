using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public sealed class MediaLibraryAvailabilityService(
    IApplicationDbContext context,
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<MediaLibraryAvailabilityService> logger) : IMediaLibraryAvailabilityService
{
    private const int InsertBatchSize = 1000;

    public async Task RebuildForLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        await context.MediaLibraryAvailabilities
            .Where(a => a.LibraryId == libraryId)
            .ExecuteDeleteAsync(cancellationToken);

        var pairs = await MediaLibraryLinkageHelper.SelectMediaLibraryPairs(context)
            .Where(p => p.LibraryId == libraryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await InsertPairsAsync(pairs, clearChangeTracker: true, cancellationToken);

        logger.LogDebug("Rebuilt media library availability for library {LibraryId} ({Count} pairs)", libraryId, pairs.Count);
    }

    public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        await context.MediaLibraryAvailabilities.ExecuteDeleteAsync(cancellationToken);

        var pairs = await MediaLibraryLinkageHelper.SelectMediaLibraryPairs(context)
            .Distinct()
            .ToListAsync(cancellationToken);

        await InsertPairsAsync(pairs, clearChangeTracker: true, cancellationToken);

        logger.LogInformation("Rebuilt media library availability for all libraries ({Count} pairs)", pairs.Count);
    }

    public async Task EnsurePopulatedAsync(CancellationToken cancellationToken = default)
    {
        if (await context.MediaLibraryAvailabilities.AnyAsync(cancellationToken))
            return;

        logger.LogInformation("Media library availability table is empty, running full rebuild");
        await RebuildAllAsync(cancellationToken);
    }

    public async Task EnsureFromIndexedFilesAsync(
        Guid libraryId,
        IReadOnlyList<Guid> indexedFileIds,
        CancellationToken cancellationToken = default)
    {
        if (indexedFileIds.Count == 0)
            return;

        var pairs = await MediaLibraryLinkageHelper
            .SelectMediaLibraryPairsForIndexedFiles(context, libraryId, indexedFileIds)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (pairs.Count == 0)
            return;

        var mediaIds = pairs.Select(p => p.MediaId).Distinct().ToList();
        var existingMediaIds = await context.MediaLibraryAvailabilities
            .AsNoTracking()
            .Where(a => a.LibraryId == libraryId && mediaIds.Contains(a.MediaId))
            .Select(a => a.MediaId)
            .ToListAsync(cancellationToken);

        var existingSet = existingMediaIds.ToHashSet();
        var missing = pairs
            .Where(p => !existingSet.Contains(p.MediaId))
            .GroupBy(p => p.MediaId)
            .Select(g => g.First())
            .ToList();

        if (missing.Count == 0)
            return;

        // Do not clear the change tracker: CreateMedia still holds tracked entities on this context.
        await InsertPairsAsync(missing, clearChangeTracker: false, cancellationToken);
        cacheInvalidator.InvalidateAll();

        logger.LogDebug(
            "Ensured media library availability for library {LibraryId} from {FileCount} files ({InsertedCount} new pairs)",
            libraryId,
            indexedFileIds.Count,
            missing.Count);
    }

    private async Task InsertPairsAsync(
        IReadOnlyList<MediaLibraryPairProjection> pairs,
        bool clearChangeTracker,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
            return;

        foreach (var batch in pairs.Chunk(InsertBatchSize))
        {
            context.MediaLibraryAvailabilities.AddRange(batch.Select(p => new MediaLibraryAvailability
            {
                LibraryId = p.LibraryId,
                MediaId = p.MediaId
            }));

            await context.SaveChangesAsync(cancellationToken);
            if (clearChangeTracker)
                ClearChangeTracker();
        }
    }

    private void ClearChangeTracker()
    {
        if (context is DbContext dbContext)
            dbContext.ChangeTracker.Clear();
    }
}
