using FluentValidation;
using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Commands.SetMediaWatchState;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record SetMediaWatchStateCommand(Guid MediaId, bool Watched, WatchStateScope Scope) : IRequest<SetMediaWatchStateResult>;

public record SetMediaWatchStateResult(IReadOnlyList<Guid> AffectedMediaIds);

public class SetMediaWatchStateCommandValidator : AbstractValidator<SetMediaWatchStateCommand>
{
    public SetMediaWatchStateCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.Scope).IsInEnum();
    }
}

public class SetMediaWatchStateCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    INextEpisodeEnqueueService nextEpisodeEnqueueService,
    IPlaybackProgressNotifier progressNotifier,
    IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<SetMediaWatchStateCommand, SetMediaWatchStateResult>
{
    public async Task<SetMediaWatchStateResult> Handle(SetMediaWatchStateCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return new SetMediaWatchStateResult([]);

        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var media = await context.Medias
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);
        ValidateScope(media, request.Scope);

        var targetMediaIds = await ResolveTargetMediaIdsAsync(media, request.Scope, cancellationToken);
        if (targetMediaIds.Count == 0)
            return new SetMediaWatchStateResult([]);

        var timeNow = DateTime.UtcNow;
        var existingStates = await context.UserMediaStates
            .Where(s => s.UserId == userId && targetMediaIds.Contains(s.MediaId))
            .ToDictionaryAsync(s => s.MediaId, cancellationToken);

        var notifications = new List<(Guid MediaId, double Progress, bool IsCompleted)>();
        Guid? episodeToEnqueue = null;

        foreach (var mediaId in targetMediaIds)
        {
            if (!existingStates.TryGetValue(mediaId, out var state))
            {
                state = new UserMediaState
                {
                    UserId = userId,
                    MediaId = mediaId,
                    PlayCount = 0,
                    IsCompleted = false,
                    LastPlaybackPosition = 0
                };
                context.UserMediaStates.Add(state);
                existingStates[mediaId] = state;
            }

            var wasCompleted = state.IsCompleted;

            if (request.Watched)
            {
                if (!wasCompleted)
                {
                    state.IsCompleted = true;
                    state.ProgressPercentage = 100;
                    state.LastPlaybackPosition = 0;
                    state.LastInteractedAt = timeNow;
                    notifications.Add((mediaId, 100, true));

                    if (request.Scope == WatchStateScope.Item
                        && mediaId == request.MediaId
                        && media.Type == MediaType.SerieEpisode)
                    {
                        episodeToEnqueue = mediaId;
                    }
                }
            }
            else if (wasCompleted || state.ProgressPercentage > 0 || state.LastPlaybackPosition > 0)
            {
                state.IsCompleted = false;
                state.ProgressPercentage = 0;
                state.LastPlaybackPosition = 0;
                state.LastInteractedAt = timeNow;
                notifications.Add((mediaId, 0, false));
            }
        }

        if (request.Watched && episodeToEnqueue is { } episodeId)
            await nextEpisodeEnqueueService.EnqueueNextEpisodeAsync(userId, episodeId, timeNow, cancellationToken);
        else if (request.Watched && request.Scope == WatchStateScope.Season)
        {
            var lastEpisodeId = await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SeasonId == request.MediaId)
                .OrderByDescending(e => e.EpisodeNumber)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastEpisodeId != default)
                await nextEpisodeEnqueueService.EnqueueNextEpisodeAsync(userId, lastEpisodeId, timeNow, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        cacheInvalidator.InvalidateAll();

        var identityUserId = currentUser.IdentityId;
        if (!string.IsNullOrEmpty(identityUserId))
        {
            foreach (var (mediaId, progress, isCompleted) in notifications)
            {
                await progressNotifier.NotifyProgressUpdatedAsync(
                    identityUserId,
                    mediaId,
                    progress,
                    isCompleted,
                    cancellationToken);
            }
        }

        return new SetMediaWatchStateResult(notifications.Select(n => n.MediaId).ToList());
    }

    private static void ValidateScope(BaseMedia media, WatchStateScope scope)
    {
        var valid = scope switch
        {
            WatchStateScope.Item => media is Movie or SerieEpisode,
            WatchStateScope.Season => media is SerieSeason,
            WatchStateScope.Serie => media is Serie,
            _ => false
        };

        if (!valid)
            throw new ValidationException([new ValidationFailure(nameof(SetMediaWatchStateCommand.Scope), "Scope does not match the media type.")]);
    }

    private async Task<List<Guid>> ResolveTargetMediaIdsAsync(
        BaseMedia media,
        WatchStateScope scope,
        CancellationToken cancellationToken)
    {
        return scope switch
        {
            WatchStateScope.Item => [media.Id],
            WatchStateScope.Season => await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SeasonId == media.Id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken),
            WatchStateScope.Serie => await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SerieId == media.Id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken),
            _ => []
        };
    }
}
