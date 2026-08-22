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
    IPlaybackBookmarkService bookmarkService,
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

        var notifications = new List<(Guid MediaId, double Progress, bool IsCompleted, MediaType MediaType)>();
        Guid? episodeToComplete = null;

        foreach (var mediaId in targetMediaIds)
        {
            if (!existingStates.TryGetValue(mediaId, out var state))
            {
                state = new UserMediaState
                {
                    UserId = userId,
                    MediaId = mediaId,
                    PlayCount = 0,
                    IsCompleted = false
                };
                context.UserMediaStates.Add(state);
                existingStates[mediaId] = state;
            }

            var wasCompleted = state.IsCompleted;
            var notifyType = mediaId == request.MediaId ? media.Type : MediaType.SerieEpisode;

            if (request.Watched)
            {
                if (!wasCompleted)
                {
                    state.IsCompleted = true;
                    state.LastInteractedAt = timeNow;
                    await bookmarkService.RemoveItemBookmarkAsync(userId, sharedProfileId: null, mediaId, cancellationToken);
                    notifications.Add((mediaId, 100, true, notifyType));

                    if (request.Scope == WatchStateScope.Item
                        && mediaId == request.MediaId
                        && media.Type == MediaType.SerieEpisode)
                    {
                        episodeToComplete = mediaId;
                    }
                }
            }
            else if (wasCompleted)
            {
                state.IsCompleted = false;
                state.LastInteractedAt = timeNow;
                await bookmarkService.RemoveItemBookmarkAsync(userId, sharedProfileId: null, mediaId, cancellationToken);
                notifications.Add((mediaId, 0, false, notifyType));
            }
        }

        if (request.Watched && episodeToComplete is { } episodeId)
            await bookmarkService.OnEpisodeCompletedAsync(userId, sharedProfileId: null, episodeId, timeNow, cancellationToken);
        else if (request.Watched && request.Scope == WatchStateScope.Season)
        {
            var lastEpisodeId = await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SeasonId == request.MediaId)
                .OrderByDescending(e => e.EpisodeNumber)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastEpisodeId != default)
                await bookmarkService.OnEpisodeCompletedAsync(userId, sharedProfileId: null, lastEpisodeId, timeNow, cancellationToken);
        }
        else if (request.Watched && request.Scope == WatchStateScope.Serie)
        {
            var lastEpisodeId = await context.Medias
                .OfType<SerieEpisode>()
                .Where(e => e.SerieId == request.MediaId)
                .OrderByDescending(e => e.Season.SeasonNumber)
                .ThenByDescending(e => e.EpisodeNumber)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (lastEpisodeId != default)
                await bookmarkService.OnEpisodeCompletedAsync(userId, sharedProfileId: null, lastEpisodeId, timeNow, cancellationToken);
        }
        else if (!request.Watched && request.Scope is WatchStateScope.Serie or WatchStateScope.Season)
        {
            var serieId = request.Scope == WatchStateScope.Serie
                ? request.MediaId
                : await context.Medias
                    .OfType<SerieSeason>()
                    .Where(s => s.Id == request.MediaId)
                    .Select(s => s.SerieId)
                    .FirstOrDefaultAsync(cancellationToken);

            if (serieId != default)
                await bookmarkService.DismissAsync(serieId, userId, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);

        cacheInvalidator.InvalidateAll();

        var identityUserId = currentUser.IdentityId;
        if (!string.IsNullOrEmpty(identityUserId))
        {
            foreach (var (mediaId, progress, isCompleted, mediaType) in notifications)
            {
                await progressNotifier.NotifyProgressUpdatedAsync(
                    identityUserId,
                    mediaId,
                    progress,
                    isCompleted,
                    mediaType,
                    cancellationToken);
            }

            if (notifications.Count > 0
                && request.Scope is WatchStateScope.Serie or WatchStateScope.Season)
            {
                await progressNotifier.NotifyProgressUpdatedAsync(
                    identityUserId,
                    request.MediaId,
                    request.Watched ? 100 : 0,
                    request.Watched,
                    media.Type,
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
