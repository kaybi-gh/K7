using K7.Server.Application.Common;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Stats.Commands.DeletePlaybackHistoryItem;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeletePlaybackHistoryItemCommand(Guid ReferenceId, bool RemoveEntirePlay = false) : IRequest;

public class DeletePlaybackHistoryItemCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IPlaybackBookmarkService bookmarkService,
    IPlaybackProgressNotifier progressNotifier) : IRequestHandler<DeletePlaybackHistoryItemCommand>
{
    public async Task Handle(DeletePlaybackHistoryItemCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            throw new ForbiddenAccessException();

        if (!await UserCapabilityEvaluator.HasAsync(
                context,
                identityService,
                userId,
                Capability.CanDeleteHistory,
                cancellationToken))
            throw new ForbiddenAccessException();

        var sessions = await context.MediaPlaybackSessions
            .Where(s => s.ReferenceId == request.ReferenceId)
            .ToListAsync(cancellationToken);

        Guard.Against.NotFound(request.ReferenceId, sessions.FirstOrDefault());

        var actorUserId = sessions[0].UserId;
        var mediaId = sessions[0].MediaId;
        var sharedProfileId = sessions
            .Select(s => s.SharedProfileId)
            .FirstOrDefault(id => id is not null);

        var coViewers = await context.MediaPlaybackSessionCoViewers
            .Where(c => c.ReferenceId == request.ReferenceId)
            .ToListAsync(cancellationToken);

        await EnsureCanDeleteAsync(
            userId,
            actorUserId,
            sharedProfileId,
            coViewers.Any(c => c.UserId == userId),
            request.RemoveEntirePlay,
            cancellationToken);

        var completed = IsCompleted(sessions);
        var skipped = IsSkipped(sessions);

        if (sharedProfileId is { } profileId)
        {
            if (request.RemoveEntirePlay)
            {
                await RemoveSharedPlayRecordAsync(
                    actorUserId,
                    profileId,
                    mediaId,
                    request.ReferenceId,
                    sessions,
                    coViewers,
                    completed,
                    skipped,
                    cancellationToken);
            }
            else
            {
                await OptOutOfSharedWatchAsync(
                    userId,
                    actorUserId,
                    profileId,
                    mediaId,
                    request.ReferenceId,
                    sessions,
                    coViewers,
                    completed,
                    skipped,
                    cancellationToken);
            }
        }
        else
        {
            await AdjustPersonalCountsAsync(
                actorUserId,
                mediaId,
                request.ReferenceId,
                completed,
                skipped,
                cancellationToken);
            context.MediaPlaybackSessionCoViewers.RemoveRange(coViewers);
            context.MediaPlaybackSessions.RemoveRange(sessions);
        }

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();

        await NotifyAsync(request.RemoveEntirePlay ? actorUserId : userId, mediaId, cancellationToken);
    }

    private async Task EnsureCanDeleteAsync(
        Guid userId,
        Guid actorUserId,
        Guid? sharedProfileId,
        bool isCoViewer,
        bool removeEntirePlay,
        CancellationToken cancellationToken)
    {
        if (removeEntirePlay)
        {
            var role = await UserCapabilityEvaluator.GetRoleAsync(context, identityService, userId, cancellationToken);
            if (role != Roles.Administrator)
                throw new ForbiddenAccessException();
            return;
        }

        if (actorUserId == userId || isCoViewer)
            return;

        if (sharedProfileId is { } profileId)
        {
            var isHost = await context.SharedProfiles
                .AsNoTracking()
                .AnyAsync(g => g.Id == profileId && g.HostUserId == userId, cancellationToken);
            if (isHost)
                return;
        }

        throw new ForbiddenAccessException();
    }

    private async Task RemoveSharedPlayRecordAsync(
        Guid actorUserId,
        Guid sharedProfileId,
        Guid mediaId,
        Guid referenceId,
        List<MediaPlaybackSession> sessions,
        List<MediaPlaybackSessionCoViewer> coViewers,
        bool completed,
        bool skipped,
        CancellationToken cancellationToken)
    {
        await AdjustSharedProfileStateAsync(
            sharedProfileId,
            mediaId,
            actorUserId,
            referenceId,
            completed,
            skipped,
            cancellationToken);
        context.MediaPlaybackSessionCoViewers.RemoveRange(coViewers);
        context.MediaPlaybackSessions.RemoveRange(sessions);
    }

    private async Task OptOutOfSharedWatchAsync(
        Guid userId,
        Guid actorUserId,
        Guid sharedProfileId,
        Guid mediaId,
        Guid referenceId,
        List<MediaPlaybackSession> sessions,
        List<MediaPlaybackSessionCoViewer> coViewers,
        bool completed,
        bool skipped,
        CancellationToken cancellationToken)
    {
        var isActor = actorUserId == userId;
        var myCoViewer = coViewers.FirstOrDefault(c => c.UserId == userId);
        var otherCoViewers = coViewers.Where(c => c.UserId != userId).ToList();

        if (!isActor && myCoViewer is null)
        {
            await RemoveSharedPlayRecordAsync(
                actorUserId,
                sharedProfileId,
                mediaId,
                referenceId,
                sessions,
                coViewers,
                completed,
                skipped,
                cancellationToken);
            return;
        }

        if (isActor && otherCoViewers.Count > 0)
        {
            var newActorId = await PreferHostCoViewerAsync(sharedProfileId, otherCoViewers, cancellationToken);
            foreach (var session in sessions)
                session.UserId = newActorId;

            var promoted = otherCoViewers.First(c => c.UserId == newActorId);
            context.MediaPlaybackSessionCoViewers.Remove(promoted);
            await AdjustPersonalCountsAfterSharedOptOutAsync(
                userId,
                mediaId,
                referenceId,
                completed,
                cancellationToken);
            return;
        }

        if (isActor)
        {
            await RemoveSharedPlayRecordAsync(
                actorUserId,
                sharedProfileId,
                mediaId,
                referenceId,
                sessions,
                coViewers,
                completed,
                skipped,
                cancellationToken);
            await AdjustPersonalCountsAfterSharedOptOutAsync(
                userId,
                mediaId,
                referenceId,
                completed,
                cancellationToken);
            return;
        }

        context.MediaPlaybackSessionCoViewers.Remove(myCoViewer!);
        await AdjustPersonalCountsAfterSharedOptOutAsync(
            userId,
            mediaId,
            referenceId,
            completed,
            cancellationToken);
    }

    private async Task<Guid> PreferHostCoViewerAsync(
        Guid sharedProfileId,
        List<MediaPlaybackSessionCoViewer> otherCoViewers,
        CancellationToken cancellationToken)
    {
        var hostUserId = await context.SharedProfiles
            .AsNoTracking()
            .Where(g => g.Id == sharedProfileId)
            .Select(g => g.HostUserId)
            .FirstOrDefaultAsync(cancellationToken);

        var hostCoViewer = otherCoViewers.FirstOrDefault(c => c.UserId == hostUserId);
        return hostCoViewer?.UserId ?? otherCoViewers[0].UserId;
    }

    private async Task AdjustPersonalCountsAsync(
        Guid userId,
        Guid mediaId,
        Guid excludedReferenceId,
        bool completed,
        bool skipped,
        CancellationToken cancellationToken)
    {
        var state = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);

        if (state is null)
            return;

        if (completed)
            state.PlayCount = Math.Max(0, state.PlayCount - 1);

        if (skipped)
            state.SkipCount = Math.Max(0, state.SkipCount - 1);

        var remainingCompleted = await HasRemainingCompletedAsync(
            userId,
            mediaId,
            excludedReferenceId,
            cancellationToken);

        if (!remainingCompleted)
            state.IsCompleted = state.PlayCount > 0;

        if (!remainingCompleted && !await HasRemainingInProgressAsync(userId, mediaId, excludedReferenceId, sharedProfileId: null, cancellationToken))
            await bookmarkService.RemoveItemBookmarkAsync(userId, sharedProfileId: null, mediaId, cancellationToken);
    }

    private async Task AdjustPersonalCountsAfterSharedOptOutAsync(
        Guid userId,
        Guid mediaId,
        Guid excludedReferenceId,
        bool completed,
        CancellationToken cancellationToken)
    {
        if (!completed)
            return;

        var remainingCompleted = await HasRemainingCompletedAsync(
            userId,
            mediaId,
            excludedReferenceId,
            cancellationToken);
        if (remainingCompleted)
            return;

        var state = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);
        if (state is null)
            return;

        state.PlayCount = Math.Max(0, state.PlayCount - 1);
        state.IsCompleted = state.PlayCount > 0;
        if (!state.IsCompleted)
            await bookmarkService.RemoveItemBookmarkAsync(userId, sharedProfileId: null, mediaId, cancellationToken);
    }

    private async Task AdjustSharedProfileStateAsync(
        Guid sharedProfileId,
        Guid mediaId,
        Guid actorUserId,
        Guid excludedReferenceId,
        bool completed,
        bool skipped,
        CancellationToken cancellationToken)
    {
        var state = await context.SharedProfileMediaStates
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.MediaId == mediaId, cancellationToken);

        if (state is null)
            return;

        if (completed)
            state.PlayCount = Math.Max(0, state.PlayCount - 1);

        if (skipped)
            state.SkipCount = Math.Max(0, state.SkipCount - 1);

        var remainingCompleted = await HasRemainingSharedCompletedAsync(
            sharedProfileId,
            mediaId,
            excludedReferenceId,
            cancellationToken);
        var remainingInProgress = await HasRemainingInProgressAsync(
            actorUserId,
            mediaId,
            excludedReferenceId,
            sharedProfileId,
            cancellationToken);

        if (!remainingCompleted)
            state.IsCompleted = state.PlayCount > 0;

        if (!remainingCompleted && !remainingInProgress)
        {
            if (state.PlayCount == 0 && state.SkipCount == 0)
            {
                context.SharedProfileMediaStates.Remove(state);
            }
            else
            {
                if (!state.IsCompleted)
                    await bookmarkService.RemoveItemBookmarkAsync(
                        userId: null,
                        sharedProfileId,
                        mediaId,
                        cancellationToken);
            }
        }
    }

    private async Task<bool> HasRemainingCompletedAsync(
        Guid userId,
        Guid mediaId,
        Guid excludedReferenceId,
        CancellationToken cancellationToken)
    {
        var coViewerReferenceIds = context.MediaPlaybackSessionCoViewers
            .Where(c => c.UserId == userId)
            .Select(c => c.ReferenceId);

        return await context.MediaPlaybackSessions
            .Where(s => s.MediaId == mediaId
                && s.ReferenceId != excludedReferenceId
                && (s.UserId == userId || coViewerReferenceIds.Contains(s.ReferenceId)))
            .AnyAsync(s => s.CompletedAt != null
                || (s.DurationSeconds > 0 && s.PositionSeconds / s.DurationSeconds >= 0.9)
                || (s.DurationSeconds > 0 && s.WatchedDurationSeconds / s.DurationSeconds >= 0.9),
                cancellationToken);
    }

    private async Task<bool> HasRemainingSharedCompletedAsync(
        Guid sharedProfileId,
        Guid mediaId,
        Guid excludedReferenceId,
        CancellationToken cancellationToken) =>
        await context.MediaPlaybackSessions
            .Where(s => s.SharedProfileId == sharedProfileId
                && s.MediaId == mediaId
                && s.ReferenceId != excludedReferenceId)
            .AnyAsync(s => s.CompletedAt != null
                || (s.DurationSeconds > 0 && s.PositionSeconds / s.DurationSeconds >= 0.9)
                || (s.DurationSeconds > 0 && s.WatchedDurationSeconds / s.DurationSeconds >= 0.9),
                cancellationToken);

    private async Task<bool> HasRemainingInProgressAsync(
        Guid userId,
        Guid mediaId,
        Guid excludedReferenceId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken)
    {
        var query = context.MediaPlaybackSessions
            .Where(s => s.MediaId == mediaId && s.ReferenceId != excludedReferenceId);

        query = sharedProfileId is { } profileId
            ? query.Where(s => s.SharedProfileId == profileId)
            : query.Where(s => s.UserId == userId && s.SharedProfileId == null);

        return await query.AnyAsync(
            s => s.CompletedAt == null && (s.PositionSeconds > 0 || s.WatchedDurationSeconds > 0),
            cancellationToken);
    }

    private async Task NotifyAsync(Guid userId, Guid mediaId, CancellationToken cancellationToken)
    {
        var identityUserId = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IdentityUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(identityUserId))
            return;

        var mediaType = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId)
            .Select(m => m.Type)
            .FirstOrDefaultAsync(cancellationToken);

        var personal = await context.UserMediaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);
        var bookmark = await context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.MediaId == mediaId, cancellationToken);

        await progressNotifier.NotifyProgressUpdatedAsync(
            identityUserId,
            mediaId,
            bookmark?.ProgressPercentage ?? (personal?.IsCompleted == true ? 100 : 0),
            personal?.IsCompleted ?? false,
            mediaType,
            cancellationToken);
    }

    private static bool IsCompleted(IReadOnlyList<MediaPlaybackSession> sessions) =>
        sessions.Any(s => s.CompletedAt != null)
        || sessions.Any(s => s.DurationSeconds > 0 && s.PositionSeconds / s.DurationSeconds >= 0.9)
        || sessions.Any(s => s.DurationSeconds > 0 && s.WatchedDurationSeconds / s.DurationSeconds >= 0.9);

    private static bool IsSkipped(IReadOnlyList<MediaPlaybackSession> sessions)
    {
        if (IsCompleted(sessions))
            return false;

        var finished = sessions.All(s =>
            s.State is PlaybackState.Ended or PlaybackState.Idle
            || s.StoppedAt != null);
        var watched = sessions.Sum(s =>
            PlaybackSkipRules.EffectiveWatchedSeconds(s.WatchedDurationSeconds, s.PositionSeconds));
        return PlaybackSkipRules.IsSkippedListen(isCompleted: false, finished, watched);
    }
}
