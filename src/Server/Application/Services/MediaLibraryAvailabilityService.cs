using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public sealed class MediaLibraryAvailabilityService(
    IApplicationDbContext context,
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

        await InsertPairsAsync(pairs, cancellationToken);

        logger.LogDebug("Rebuilt media library availability for library {LibraryId} ({Count} pairs)", libraryId, pairs.Count);
    }

    public async Task RebuildAllAsync(CancellationToken cancellationToken = default)
    {
        await context.MediaLibraryAvailabilities.ExecuteDeleteAsync(cancellationToken);

        var pairs = await MediaLibraryLinkageHelper.SelectMediaLibraryPairs(context)
            .Distinct()
            .ToListAsync(cancellationToken);

        await InsertPairsAsync(pairs, cancellationToken);

        logger.LogInformation("Rebuilt media library availability for all libraries ({Count} pairs)", pairs.Count);
    }

    public async Task EnsurePopulatedAsync(CancellationToken cancellationToken = default)
    {
        if (await context.MediaLibraryAvailabilities.AnyAsync(cancellationToken))
            return;

        logger.LogInformation("Media library availability table is empty, running full rebuild");
        await RebuildAllAsync(cancellationToken);
    }

    private async Task InsertPairsAsync(IReadOnlyList<MediaLibraryPair> pairs, CancellationToken cancellationToken)
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
            ClearChangeTracker();
        }
    }

    private void ClearChangeTracker()
    {
        if (context is DbContext dbContext)
            dbContext.ChangeTracker.Clear();
    }
}
