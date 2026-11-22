using System.Text;
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
    private const int BatchSize = 500;

    // ON CONFLICT DO NOTHING is understood by both Postgres and SQLite. It makes the insert
    // idempotent in the database, so writers racing on the same pair never fail a batch.
    private const string InsertSqlPrefix =
        $"INSERT INTO \"{nameof(IApplicationDbContext.MediaLibraryAvailabilities)}\" "
        + $"(\"{nameof(MediaLibraryAvailability.LibraryId)}\", \"{nameof(MediaLibraryAvailability.MediaId)}\") VALUES ";

    private const string InsertSqlSuffix = " ON CONFLICT DO NOTHING";

    public async Task RebuildForLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        var (inserted, removed) = await SyncAsync(libraryId, cancellationToken);

        logger.LogDebug(
            "Rebuilt media library availability for library {LibraryId} ({InsertedCount} inserted, {RemovedCount} removed)",
            libraryId,
            inserted,
            removed);
    }

    public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        var (inserted, removed) = await SyncAsync(libraryId: null, cancellationToken);

        logger.LogInformation(
            "Rebuilt media library availability for all libraries ({InsertedCount} inserted, {RemovedCount} removed)",
            inserted,
            removed);
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
            .Select(p => new MediaLibraryPair(p.LibraryId, p.MediaId))
            .Distinct()
            .ToList();

        if (missing.Count == 0)
            return;

        var inserted = await InsertPairsAsync(missing, cancellationToken);
        cacheInvalidator.InvalidateAll();

        logger.LogDebug(
            "Ensured media library availability for library {LibraryId} from {FileCount} files ({InsertedCount} new pairs)",
            libraryId,
            indexedFileIds.Count,
            inserted);
    }

    /// <summary>
    /// Inserts the given pairs, skipping the ones another writer already inserted.
    /// Returns the number of rows actually written.
    /// </summary>
    internal async Task<int> InsertPairsAsync(
        IReadOnlyCollection<MediaLibraryPair> pairs,
        CancellationToken cancellationToken = default)
    {
        if (pairs.Count == 0)
            return 0;

        if (context is not DbContext dbContext)
            throw new InvalidOperationException("Media library availability writes require a relational DbContext.");

        var inserted = 0;

        foreach (var batch in pairs.Distinct().Chunk(BatchSize))
        {
            var sql = new StringBuilder(InsertSqlPrefix);
            var parameters = new object[batch.Length * 2];

            for (var i = 0; i < batch.Length; i++)
            {
                if (i > 0)
                    sql.Append(", ");

                sql.Append("({").Append(i * 2).Append("}, {").Append((i * 2) + 1).Append("})");
                parameters[i * 2] = batch[i].LibraryId;
                parameters[(i * 2) + 1] = batch[i].MediaId;
            }

            sql.Append(InsertSqlSuffix);

            inserted += await dbContext.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
        }

        return inserted;
    }

    /// <summary>
    /// Brings the table in line with the current file links by writing only the difference.
    /// Rows that are still valid are never deleted, so a library stays browsable during a rebuild.
    /// </summary>
    private async Task<(int Inserted, int Removed)> SyncAsync(Guid? libraryId, CancellationToken cancellationToken)
    {
        var existingQuery = context.MediaLibraryAvailabilities.AsNoTracking();
        var desiredQuery = MediaLibraryLinkageHelper.SelectMediaLibraryPairs(context);

        if (libraryId is Guid scopedLibraryId)
        {
            existingQuery = existingQuery.Where(a => a.LibraryId == scopedLibraryId);
            desiredQuery = desiredQuery.Where(p => p.LibraryId == scopedLibraryId);
        }

        // Existing rows are read before the desired pairs on purpose. A row inserted concurrently by
        // EnsureFromIndexedFilesAsync is then either absent from the existing set, or its file link was
        // committed before the desired pairs are read. Either way it is not mistaken for a stale row.
        var existingRows = await existingQuery
            .Select(a => new { a.LibraryId, a.MediaId })
            .ToListAsync(cancellationToken);
        var desiredRows = await desiredQuery
            .Distinct()
            .ToListAsync(cancellationToken);

        var existing = existingRows.Select(a => new MediaLibraryPair(a.LibraryId, a.MediaId)).ToHashSet();
        var desired = desiredRows.Select(p => new MediaLibraryPair(p.LibraryId, p.MediaId)).ToHashSet();

        var stale = existing.Where(p => !desired.Contains(p)).ToList();
        var missing = desired.Where(p => !existing.Contains(p)).ToList();

        var removed = await DeletePairsAsync(stale, cancellationToken);
        var inserted = await InsertPairsAsync(missing, cancellationToken);

        return (inserted, removed);
    }

    private async Task<int> DeletePairsAsync(
        IReadOnlyCollection<MediaLibraryPair> pairs,
        CancellationToken cancellationToken)
    {
        var removed = 0;

        foreach (var libraryPairs in pairs.GroupBy(p => p.LibraryId))
        {
            var libraryId = libraryPairs.Key;

            foreach (var batch in libraryPairs.Select(p => p.MediaId).Chunk(BatchSize))
            {
                var mediaIds = batch.ToList();
                removed += await context.MediaLibraryAvailabilities
                    .Where(a => a.LibraryId == libraryId && mediaIds.Contains(a.MediaId))
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        return removed;
    }
}
