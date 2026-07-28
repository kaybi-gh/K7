using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

/// <summary>
/// Boosts the pending tasks of a media on demand.
/// </summary>
/// <remarks>
/// This is what makes the queue feel alive during a large first index: whatever a user opens or presses
/// play on jumps ahead of the backlog. The update is scoped to one or two target entity ids and backed
/// by IX_BackgroundTasks_TargetEntityId, so it stays a handful of rows. A broader re-scoring pass would
/// churn the scheduling index, contend with the scan writer on SQLite and risk update-update deadlocks
/// on Postgres, which is why the score is otherwise only ever set at enqueue time.
/// </remarks>
public sealed class PlaybackBoostService(
    IApplicationDbContext context,
    IBackgroundTaskQueue taskQueue,
    ILogger<PlaybackBoostService> logger) : IPlaybackBoostService
{
    public async Task BoostPendingWorkAsync(Guid indexedFileId, Guid? mediaId, CancellationToken cancellationToken = default)
    {
        var targetIds = mediaId.HasValue
            ? new[] { indexedFileId, mediaId.Value }
            : [indexedFileId];

        var boosted = await context.BackgroundTasks
            .Where(t => t.TargetEntityId != null
                && targetIds.Contains(t.TargetEntityId.Value)
                && (t.Status == BackgroundTaskStatus.Pending || t.Status == BackgroundTaskStatus.WaitingForRetry)
                && t.Priority < BackgroundTaskScheduling.OnDemandBoost)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Priority, BackgroundTaskScheduling.OnDemandBoost),
                cancellationToken);

        if (boosted == 0)
            return;

        logger.LogInformation(
            "Boosted {Count} pending task(s) on demand for IndexedFile {IndexedFileId}",
            boosted, indexedFileId);

        // Without an explicit signal the boosted task would only be picked at the next natural wake-up,
        // which can be up to the orphan poll interval away.
        taskQueue.Enqueue(Guid.Empty);
    }
}
