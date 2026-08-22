using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Requests;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Users.Commands.BulkUpsertMediaStates;

[Authorize(Roles = Roles.Administrator)]
public record BulkUpsertMediaStatesCommand : IRequest<int>
{
    public required Guid UserId { get; init; }
    public required IReadOnlyList<BulkUpsertMediaStatesRequest.MediaStateItem> Items { get; init; }
    public MergeStrategy? Strategy { get; init; }
}

public class BulkUpsertMediaStatesCommandHandler(
    IApplicationDbContext context,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IPlaybackBookmarkService bookmarkService)
    : IRequestHandler<BulkUpsertMediaStatesCommand, int>
{
    public async Task<int> Handle(BulkUpsertMediaStatesCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        Guard.Against.NotFound(request.UserId, user);

        var mediaIds = request.Items.Select(i => i.MediaId).Distinct().ToList();

        var existingStates = (await context.UserMediaStates
                .Where(s => s.UserId == request.UserId && mediaIds.Contains(s.MediaId))
                .ToListAsync(cancellationToken))
            .GroupBy(s => s.MediaId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.LastInteractedAt).First());

        var upsertedCount = 0;
        var strategy = request.Strategy ?? new MergeStrategy();
        var timeNow = DateTime.UtcNow;

        foreach (var item in request.Items)
        {
            if (existingStates.TryGetValue(item.MediaId, out var existing))
            {
                var updated = false;

                if (strategy.PlayCount is PlayCountMergeMode.Additive)
                {
                    existing.PlayCount += item.PlayCount;
                    updated = true;
                }
                else if (strategy.PlayCount is PlayCountMergeMode.Max && item.PlayCount > existing.PlayCount)
                {
                    existing.PlayCount = item.PlayCount;
                    updated = true;
                }

                if (strategy.Progress is ProgressConflictMode.AlwaysOverwrite)
                {
                    existing.IsCompleted = item.IsCompleted;
                    existing.LastInteractedAt = item.LastInteractedAt;
                    updated = true;
                }
                else if (item.LastInteractedAt.HasValue &&
                    (existing.LastInteractedAt is null || item.LastInteractedAt.Value > existing.LastInteractedAt.Value))
                {
                    existing.IsCompleted = item.IsCompleted;
                    existing.LastInteractedAt = item.LastInteractedAt;
                    updated = true;
                }

                if (updated)
                {
                    await ApplyBookmarkFromImportAsync(
                        request.UserId,
                        item,
                        existing.IsCompleted,
                        timeNow,
                        cancellationToken);
                    upsertedCount++;
                }
            }
            else
            {
                var state = new UserMediaState
                {
                    UserId = request.UserId,
                    MediaId = item.MediaId,
                    PlayCount = item.PlayCount,
                    IsCompleted = item.IsCompleted,
                    LastInteractedAt = item.LastInteractedAt
                };
                context.UserMediaStates.Add(state);
                existingStates[item.MediaId] = state;
                await ApplyBookmarkFromImportAsync(
                    request.UserId,
                    item,
                    item.IsCompleted,
                    timeNow,
                    cancellationToken);
                upsertedCount++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (upsertedCount > 0)
        {
            await EnqueueSeriesBookmarksAsync(request.UserId, existingStates, timeNow, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            cacheInvalidator.InvalidateAll();
        }

        return upsertedCount;
    }

    private async Task ApplyBookmarkFromImportAsync(
        Guid userId,
        BulkUpsertMediaStatesRequest.MediaStateItem item,
        bool isCompleted,
        DateTime timeNow,
        CancellationToken cancellationToken)
    {
        if (isCompleted)
        {
            await bookmarkService.RemoveItemBookmarkAsync(userId, sharedProfileId: null, item.MediaId, cancellationToken);
            var isEpisode = await context.Medias
                .OfType<SerieEpisode>()
                .AnyAsync(e => e.Id == item.MediaId, cancellationToken);
            if (isEpisode)
            {
                await bookmarkService.OnEpisodeCompletedAsync(
                    userId,
                    sharedProfileId: null,
                    item.MediaId,
                    item.LastInteractedAt ?? timeNow,
                    cancellationToken);
            }

            return;
        }

        if (item.LastPlaybackPosition > 0 || item.ProgressPercentage > 0)
        {
            var duration = item.ProgressPercentage > 0
                ? item.LastPlaybackPosition / (item.ProgressPercentage / 100.0)
                : 0;
            await bookmarkService.UpsertItemBookmarkAsync(
                userId,
                sharedProfileId: null,
                item.MediaId,
                item.LastPlaybackPosition,
                duration,
                item.LastInteractedAt ?? timeNow,
                cancellationToken);
        }
    }

    private async Task EnqueueSeriesBookmarksAsync(
        Guid userId,
        Dictionary<Guid, UserMediaState> states,
        DateTime timeNow,
        CancellationToken cancellationToken)
    {
        var completedIds = states
            .Where(kv => kv.Value.IsCompleted)
            .Select(kv => kv.Key)
            .ToList();
        if (completedIds.Count == 0)
            return;

        var completedEpisodes = await context.Medias
            .OfType<SerieEpisode>()
            .Where(e => completedIds.Contains(e.Id))
            .Select(e => new { e.Id, e.SerieId, SeasonNumber = e.Season.SeasonNumber, e.EpisodeNumber })
            .ToListAsync(cancellationToken);
        if (completedEpisodes.Count == 0)
            return;

        var latestBySerie = completedEpisodes
            .GroupBy(e => e.SerieId)
            .Select(g => g
                .OrderByDescending(e => e.SeasonNumber == 0 ? int.MinValue : e.SeasonNumber)
                .ThenByDescending(e => e.EpisodeNumber)
                .First());

        foreach (var episode in latestBySerie)
        {
            var interactedAt = states[episode.Id].LastInteractedAt ?? timeNow;
            await bookmarkService.OnEpisodeCompletedAsync(
                userId,
                sharedProfileId: null,
                episode.Id,
                interactedAt,
                cancellationToken);
        }
    }
}
