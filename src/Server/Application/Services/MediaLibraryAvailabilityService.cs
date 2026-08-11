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

        // Collapse accidental duplicates inside one writer; concurrent writers still need conflict handling below.
        var uniquePairs = pairs
            .GroupBy(p => (p.LibraryId, p.MediaId))
            .Select(g => g.First())
            .ToList();

        foreach (var batch in uniquePairs.Chunk(InsertBatchSize))
        {
            var entities = batch.Select(p => new MediaLibraryAvailability
            {
                LibraryId = p.LibraryId,
                MediaId = p.MediaId
            }).ToList();

            context.MediaLibraryAvailabilities.AddRange(entities);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateAvailability(ex))
            {
                // Rebuild (scan) races Ensure (CreateMedia), and two Ensures can race on shared
                // parents (serie/album/artist). Detach the batch and insert one-by-one like
                // CreateBackgroundTasksBatch does for unique active-task races.
                DetachAvailabilities(entities);

                foreach (var entity in entities)
                {
                    var exists = await context.MediaLibraryAvailabilities
                        .AsNoTracking()
                        .AnyAsync(
                            a => a.LibraryId == entity.LibraryId && a.MediaId == entity.MediaId,
                            cancellationToken);
                    if (exists)
                        continue;

                    context.MediaLibraryAvailabilities.Add(entity);
                    try
                    {
                        await context.SaveChangesAsync(cancellationToken);
                    }
                    catch (DbUpdateException rowEx) when (IsDuplicateAvailability(rowEx))
                    {
                        DetachAvailabilities([entity]);
                    }
                }
            }

            if (clearChangeTracker)
                ClearChangeTracker();
        }
    }

    private void DetachAvailabilities(IEnumerable<MediaLibraryAvailability> entities)
    {
        foreach (var entity in entities)
        {
            var entry = context.Entry(entity);
            if (entry.State != EntityState.Detached)
                entry.State = EntityState.Detached;
        }
    }

    private void ClearChangeTracker()
    {
        if (context is DbContext dbContext)
            dbContext.ChangeTracker.Clear();
    }

    private static bool IsDuplicateAvailability(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("PK_MediaLibraryAvailabilities", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true;
}
