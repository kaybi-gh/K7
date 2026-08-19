using K7.Server.Application.Common;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Stats.Commands.ReassignPlaybackHistoryItem;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record ReassignPlaybackHistoryItemCommand(Guid ReferenceId, Guid? SharedProfileId, bool AsAdministrator = false) : IRequest;

public class ReassignPlaybackHistoryItemCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IPlaybackProgressNotifier progressNotifier) : IRequestHandler<ReassignPlaybackHistoryItemCommand>
{
    public async Task Handle(ReassignPlaybackHistoryItemCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            throw new ForbiddenAccessException();

        if (!await UserCapabilityEvaluator.HasAsync(
                context,
                identityService,
                userId,
                Capability.CanReassignHistory,
                cancellationToken))
            throw new ForbiddenAccessException();

        var sessions = await context.MediaPlaybackSessions
            .Where(s => s.ReferenceId == request.ReferenceId)
            .ToListAsync(cancellationToken);

        Guard.Against.NotFound(request.ReferenceId, sessions.FirstOrDefault());

        var actorUserId = sessions[0].UserId;
        var mediaId = sessions[0].MediaId;
        var currentProfileId = sessions
            .Select(s => s.SharedProfileId)
            .FirstOrDefault(id => id is not null);

        if (currentProfileId == request.SharedProfileId)
            return;

        var memberships = await context.SharedProfiles
            .AsNoTracking()
            .Where(g => g.HostUserId == userId || g.Members.Any(m => m.UserId == userId))
            .Select(g => new { g.Id, g.HostUserId })
            .ToListAsync(cancellationToken);

        var memberProfileIds = memberships.Select(g => g.Id).ToHashSet();
        var hostedProfileIds = memberships
            .Where(g => g.HostUserId == userId)
            .Select(g => g.Id)
            .ToHashSet();

        var isAdmin = request.AsAdministrator
            && await UserCapabilityEvaluator.GetRoleAsync(context, identityService, userId, cancellationToken)
                == Roles.Administrator;

        if (request.AsAdministrator && !isAdmin)
            throw new ForbiddenAccessException();

        EnsureCanReassign(
            userId,
            actorUserId,
            currentProfileId,
            request.SharedProfileId,
            memberProfileIds,
            hostedProfileIds,
            isAdmin);

        if (currentProfileId is { } fromProfileId)
            await DetachFromSharedProfileAsync(sessions, fromProfileId, actorUserId, mediaId, cancellationToken);

        if (request.SharedProfileId is { } toProfileId)
            await AttachToSharedProfileAsync(sessions, toProfileId, actorUserId, mediaId, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();

        await NotifyActorAsync(actorUserId, mediaId, cancellationToken);
    }

    private static void EnsureCanReassign(
        Guid userId,
        Guid actorUserId,
        Guid? currentProfileId,
        Guid? targetProfileId,
        IReadOnlySet<Guid> memberProfileIds,
        IReadOnlySet<Guid> hostedProfileIds,
        bool isAdmin)
    {
        if (isAdmin)
            return;

        if (targetProfileId is { } target && !memberProfileIds.Contains(target))
            throw new ForbiddenAccessException();

        if (currentProfileId is null)
        {
            if (actorUserId != userId)
                throw new ForbiddenAccessException();
            return;
        }

        if (hostedProfileIds.Contains(currentProfileId.Value))
            return;

        if (actorUserId == userId && memberProfileIds.Contains(currentProfileId.Value))
            return;

        throw new ForbiddenAccessException();
    }

    private async Task AttachToSharedProfileAsync(
        IReadOnlyList<MediaPlaybackSession> sessions,
        Guid sharedProfileId,
        Guid actorUserId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var group = await context.SharedProfiles
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == sharedProfileId, cancellationToken);
        Guard.Against.NotFound(sharedProfileId, group);

        foreach (var session in sessions)
        {
            session.SharedProfileId = group.Id;
            session.SharedProfileNameSnapshot = group.Name;
        }

        var coViewerIds = ResolveCoViewerIds(group, actorUserId);
        await EnsureCoViewersAsync(sessions[0].ReferenceId, coViewerIds, cancellationToken);

        var completed = IsCompleted(sessions);
        var latest = Latest(sessions);
        await UpsertSharedMediaStateAsync(
            sharedProfileId,
            mediaId,
            completed,
            latest,
            cancellationToken);

        if (completed)
        {
            var memberIds = group.Members
                .Select(m => m.UserId)
                .Append(group.HostUserId)
                .Distinct()
                .ToList();
            await MarkMembersWatchedAsync(memberIds, mediaId, latest.LastUpdateAt ?? latest.StartedAt, cancellationToken);
        }
        else
        {
            await ClearPersonalContinueWatchingAsync(actorUserId, mediaId, cancellationToken);
        }
    }

    private async Task DetachFromSharedProfileAsync(
        IReadOnlyList<MediaPlaybackSession> sessions,
        Guid sharedProfileId,
        Guid actorUserId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var group = await context.SharedProfiles
            .AsNoTracking()
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == sharedProfileId, cancellationToken);

        foreach (var session in sessions)
        {
            if (session.SharedProfileId != sharedProfileId)
                continue;

            session.SharedProfileId = null;
            session.SharedProfileNameSnapshot = null;
        }

        if (group is not null)
        {
            var memberIds = group.Members
                .Select(m => m.UserId)
                .Append(group.HostUserId)
                .Where(id => id != actorUserId)
                .Distinct()
                .ToList();

            if (memberIds.Count > 0)
            {
                var referenceId = sessions[0].ReferenceId;
                var coViewers = await context.MediaPlaybackSessionCoViewers
                    .Where(c => c.ReferenceId == referenceId && memberIds.Contains(c.UserId))
                    .ToListAsync(cancellationToken);
                context.MediaPlaybackSessionCoViewers.RemoveRange(coViewers);
            }
        }

        await RebuildSharedMediaStateAsync(sharedProfileId, mediaId, sessions[0].ReferenceId, cancellationToken);

        if (!IsCompleted(sessions))
            await RestorePersonalContinueWatchingAsync(actorUserId, mediaId, Latest(sessions), cancellationToken);
    }

    private async Task EnsureCoViewersAsync(
        Guid referenceId,
        IReadOnlyList<Guid> coViewerUserIds,
        CancellationToken cancellationToken)
    {
        if (coViewerUserIds.Count == 0)
            return;

        var existing = await context.MediaPlaybackSessionCoViewers
            .Where(c => c.ReferenceId == referenceId)
            .Select(c => c.UserId)
            .ToListAsync(cancellationToken);

        foreach (var coViewerUserId in coViewerUserIds.Where(id => !existing.Contains(id)))
        {
            context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
            {
                ReferenceId = referenceId,
                UserId = coViewerUserId
            });
        }
    }

    private async Task UpsertSharedMediaStateAsync(
        Guid sharedProfileId,
        Guid mediaId,
        bool completed,
        MediaPlaybackSession latest,
        CancellationToken cancellationToken)
    {
        var state = await context.SharedProfileMediaStates
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.MediaId == mediaId, cancellationToken);

        if (state is null)
        {
            state = new SharedProfileMediaState
            {
                SharedProfileId = sharedProfileId,
                MediaId = mediaId
            };
            context.SharedProfileMediaStates.Add(state);
        }

        ApplyPlayOutcome(state, completed, latest);
    }

    private async Task RebuildSharedMediaStateAsync(
        Guid sharedProfileId,
        Guid mediaId,
        Guid excludedReferenceId,
        CancellationToken cancellationToken)
    {
        var remaining = await context.MediaPlaybackSessions
            .Where(s => s.SharedProfileId == sharedProfileId
                && s.MediaId == mediaId
                && s.ReferenceId != excludedReferenceId)
            .ToListAsync(cancellationToken);

        var state = await context.SharedProfileMediaStates
            .FirstOrDefaultAsync(s => s.SharedProfileId == sharedProfileId && s.MediaId == mediaId, cancellationToken);

        if (remaining.Count == 0)
        {
            if (state is not null)
                context.SharedProfileMediaStates.Remove(state);
            return;
        }

        if (state is null)
        {
            state = new SharedProfileMediaState
            {
                SharedProfileId = sharedProfileId,
                MediaId = mediaId
            };
            context.SharedProfileMediaStates.Add(state);
        }

        ApplyPlayOutcome(state, IsCompleted(remaining), Latest(remaining));
    }

    private async Task MarkMembersWatchedAsync(
        IReadOnlyList<Guid> memberUserIds,
        Guid mediaId,
        DateTime timeNow,
        CancellationToken cancellationToken)
    {
        var existingStates = await context.UserMediaStates
            .Where(s => memberUserIds.Contains(s.UserId) && s.MediaId == mediaId)
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        foreach (var memberId in memberUserIds)
        {
            if (existingStates.TryGetValue(memberId, out var state))
            {
                if (!state.IsCompleted)
                    state.PlayCount++;

                state.IsCompleted = true;
                state.ProgressPercentage = 100;
                state.LastPlaybackPosition = 0;
                state.LastInteractedAt = timeNow;
                state.ExcludedFromContinueWatching = false;
                continue;
            }

            context.UserMediaStates.Add(new UserMediaState
            {
                UserId = memberId,
                MediaId = mediaId,
                PlayCount = 1,
                IsCompleted = true,
                ProgressPercentage = 100,
                LastPlaybackPosition = 0,
                LastInteractedAt = timeNow
            });
        }
    }

    private async Task ClearPersonalContinueWatchingAsync(
        Guid userId,
        Guid mediaId,
        CancellationToken cancellationToken)
    {
        var state = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);

        if (state is null || state.IsCompleted)
            return;

        state.LastPlaybackPosition = 0;
        state.ProgressPercentage = 0;
        state.ExcludedFromContinueWatching = true;
    }

    private async Task RestorePersonalContinueWatchingAsync(
        Guid userId,
        Guid mediaId,
        MediaPlaybackSession latest,
        CancellationToken cancellationToken)
    {
        var state = await context.UserMediaStates
            .FirstOrDefaultAsync(s => s.UserId == userId && s.MediaId == mediaId, cancellationToken);

        if (state is not null && state.IsCompleted)
            return;

        var duration = latest.DurationSeconds;
        var position = latest.PositionSeconds > 0 ? latest.PositionSeconds : latest.WatchedDurationSeconds;
        var progress = duration > 0 ? Math.Clamp(position / duration * 100, 0, 100) : 0;
        var interactedAt = latest.LastUpdateAt ?? latest.StartedAt;

        if (state is null)
        {
            context.UserMediaStates.Add(new UserMediaState
            {
                UserId = userId,
                MediaId = mediaId,
                LastPlaybackPosition = position,
                ProgressPercentage = progress,
                LastKnownDurationSeconds = duration,
                LastInteractedAt = interactedAt,
                ExcludedFromContinueWatching = false
            });
            return;
        }

        state.LastPlaybackPosition = position;
        state.ProgressPercentage = progress;
        state.LastKnownDurationSeconds = duration;
        state.LastInteractedAt = interactedAt;
        state.ExcludedFromContinueWatching = false;
    }

    private async Task NotifyActorAsync(Guid actorUserId, Guid mediaId, CancellationToken cancellationToken)
    {
        var identityUserId = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == actorUserId)
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
            .FirstOrDefaultAsync(s => s.UserId == actorUserId && s.MediaId == mediaId, cancellationToken);

        await progressNotifier.NotifyProgressUpdatedAsync(
            identityUserId,
            mediaId,
            personal?.ProgressPercentage ?? 0,
            personal?.IsCompleted ?? false,
            mediaType,
            cancellationToken);
    }

    private static void ApplyPlayOutcome(
        SharedProfileMediaState state,
        bool completed,
        MediaPlaybackSession latest)
    {
        var duration = latest.DurationSeconds;
        var position = latest.PositionSeconds > 0 ? latest.PositionSeconds : latest.WatchedDurationSeconds;
        var interactedAt = latest.LastUpdateAt ?? latest.StartedAt;

        state.LastInteractedAt = interactedAt;
        state.LastKnownDurationSeconds = duration;
        state.ExcludedFromContinueWatching = false;

        if (completed)
        {
            if (!state.IsCompleted)
                state.PlayCount++;

            state.IsCompleted = true;
            state.ProgressPercentage = 100;
            state.LastPlaybackPosition = 0;
            return;
        }

        state.IsCompleted = false;
        state.LastPlaybackPosition = position;
        state.ProgressPercentage = duration > 0
            ? Math.Clamp(position / duration * 100, 0, 100)
            : 0;
    }

    private static List<Guid> ResolveCoViewerIds(SharedProfile group, Guid actorUserId)
    {
        var coViewers = group.Members
            .Where(m => m.UserId != actorUserId)
            .Select(m => m.UserId)
            .ToList();

        if (group.HostUserId != actorUserId && !coViewers.Contains(group.HostUserId))
            coViewers.Add(group.HostUserId);

        return coViewers;
    }

    private static MediaPlaybackSession Latest(IReadOnlyList<MediaPlaybackSession> sessions) =>
        sessions
            .OrderByDescending(s => s.LastUpdateAt ?? s.StartedAt)
            .First();

    private static bool IsCompleted(IReadOnlyList<MediaPlaybackSession> sessions) =>
        sessions.Any(s => s.CompletedAt != null)
        || sessions.Any(s => s.DurationSeconds > 0 && s.PositionSeconds / s.DurationSeconds >= 0.9)
        || sessions.Any(s => s.DurationSeconds > 0 && s.WatchedDurationSeconds / s.DurationSeconds >= 0.9);
}
