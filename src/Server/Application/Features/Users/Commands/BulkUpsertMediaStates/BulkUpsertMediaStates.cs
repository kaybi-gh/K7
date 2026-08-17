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
    INextEpisodeEnqueueService nextEpisodeEnqueueService)
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
                // Ignore: keep existing play count as-is

                if (strategy.Progress is ProgressConflictMode.AlwaysOverwrite)
                {
                    existing.LastPlaybackPosition = item.LastPlaybackPosition;
                    existing.ProgressPercentage = item.ProgressPercentage;
                    existing.IsCompleted = item.IsCompleted;
                    existing.LastInteractedAt = item.LastInteractedAt;
                    updated = true;
                }
                else if (item.LastInteractedAt.HasValue &&
                    (existing.LastInteractedAt is null || item.LastInteractedAt.Value > existing.LastInteractedAt.Value))
                {
                    existing.LastPlaybackPosition = item.LastPlaybackPosition;
                    existing.ProgressPercentage = item.ProgressPercentage;
                    existing.IsCompleted = item.IsCompleted;
                    existing.LastInteractedAt = item.LastInteractedAt;
                    updated = true;
                }

                if (updated) upsertedCount++;
            }
            else
            {
                var state = new UserMediaState
                {
                    UserId = request.UserId,
                    MediaId = item.MediaId,
                    PlayCount = item.PlayCount,
                    LastPlaybackPosition = item.LastPlaybackPosition,
                    ProgressPercentage = item.ProgressPercentage,
                    IsCompleted = item.IsCompleted,
                    LastInteractedAt = item.LastInteractedAt
                };
                context.UserMediaStates.Add(state);
                existingStates[item.MediaId] = state;
                upsertedCount++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (upsertedCount > 0)
        {
            await EnqueueNextEpisodesAsync(request.UserId, existingStates, cancellationToken);
            cacheInvalidator.InvalidateAll();
        }

        return upsertedCount;
    }

    private async Task EnqueueNextEpisodesAsync(
        Guid userId,
        Dictionary<Guid, UserMediaState> states,
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
            var interactedAt = states[episode.Id].LastInteractedAt ?? DateTime.UtcNow;
            await nextEpisodeEnqueueService.EnqueueNextEpisodeAsync(userId, episode.Id, interactedAt, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
