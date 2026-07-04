using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Shared.Dtos.Requests;
using Microsoft.EntityFrameworkCore;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.MergeUsers;

[Authorize(Roles = Roles.Administrator)]
public record MergeUsersCommand(Guid SourceUserId, Guid TargetUserId, MergeStrategy? Strategy = null) : IRequest;

public class MergeUsersCommandHandler(IApplicationDbContext context, IIdentityService identityService, IUser user)
    : IRequestHandler<MergeUsersCommand>
{
    public async Task Handle(MergeUsersCommand request, CancellationToken cancellationToken)
    {
        var source = await context.Users.FirstOrDefaultAsync(u => u.Id == request.SourceUserId, cancellationToken);
        Guard.Against.NotFound(request.SourceUserId, source);

        var target = await context.Users.FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);
        Guard.Against.NotFound(request.TargetUserId, target);

        if (source.IdentityUserId == user.IdentityId)
            throw new ValidationException(
            [
                new ValidationFailure("SourceUserId", "Cannot merge your own account as source.")
            ]);

        if (source.IdentityUserId is not null)
        {
            var roles = await identityService.GetRolesAsync(source.IdentityUserId);
            if (roles.Contains(Roles.Guest))
            {
                throw new ValidationException(
                [
                    new ValidationFailure("SourceUserId", "Cannot merge the guest account.")
                ]);
            }
        }

        await MergeMediaStatesAsync(request.SourceUserId, request.TargetUserId, request.Strategy, cancellationToken);
        await MergeRatingsAsync(request.SourceUserId, request.TargetUserId, request.Strategy, cancellationToken);
        await TransferPlaylistsAsync(request.SourceUserId, request.TargetUserId, cancellationToken);
        await TransferPlaybackSessionsAsync(request.SourceUserId, request.TargetUserId, cancellationToken);

        if (source.IdentityUserId is not null)
        {
            await identityService.DeleteUserAsync(source.IdentityUserId);
        }

        context.Users.Remove(source);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task MergeMediaStatesAsync(Guid sourceUserId, Guid targetUserId, MergeStrategy? mergeStrategy, CancellationToken cancellationToken)
    {
        var sourceStates = await context.UserMediaStates
            .Where(s => s.UserId == sourceUserId)
            .ToListAsync(cancellationToken);

        if (sourceStates.Count == 0) return;

        var targetMediaIds = sourceStates.Select(s => s.MediaId).ToList();
        var targetStates = await context.UserMediaStates
            .Where(s => s.UserId == targetUserId && targetMediaIds.Contains(s.MediaId))
            .ToDictionaryAsync(s => s.MediaId, cancellationToken);

        foreach (var sourceState in sourceStates)
        {
            if (targetStates.TryGetValue(sourceState.MediaId, out var targetState))
            {
                var strategy = mergeStrategy ?? new MergeStrategy();

                if (strategy.PlayCount is PlayCountMergeMode.Additive)
                {
                    targetState.PlayCount += sourceState.PlayCount;
                }
                else if (sourceState.PlayCount > targetState.PlayCount)
                {
                    targetState.PlayCount = sourceState.PlayCount;
                }

                if (strategy.Progress is ProgressConflictMode.AlwaysOverwrite)
                {
                    targetState.LastPlaybackPosition = sourceState.LastPlaybackPosition;
                    targetState.ProgressPercentage = sourceState.ProgressPercentage;
                    targetState.IsCompleted = sourceState.IsCompleted;
                    targetState.LastInteractedAt = sourceState.LastInteractedAt;
                }
                else if (sourceState.LastInteractedAt.HasValue &&
                    (targetState.LastInteractedAt is null || sourceState.LastInteractedAt.Value > targetState.LastInteractedAt.Value))
                {
                    targetState.LastPlaybackPosition = sourceState.LastPlaybackPosition;
                    targetState.ProgressPercentage = sourceState.ProgressPercentage;
                    targetState.IsCompleted = sourceState.IsCompleted;
                    targetState.LastInteractedAt = sourceState.LastInteractedAt;
                }
            }
            else
            {
                context.UserMediaStates.Add(new UserMediaState
                {
                    UserId = targetUserId,
                    MediaId = sourceState.MediaId,
                    PlayCount = sourceState.PlayCount,
                    LastPlaybackPosition = sourceState.LastPlaybackPosition,
                    ProgressPercentage = sourceState.ProgressPercentage,
                    IsCompleted = sourceState.IsCompleted,
                    LastInteractedAt = sourceState.LastInteractedAt
                });
            }

            context.UserMediaStates.Remove(sourceState);
        }
    }

    private async Task MergeRatingsAsync(Guid sourceUserId, Guid targetUserId, MergeStrategy? mergeStrategy, CancellationToken cancellationToken)
    {
        var sourceRatings = await context.Ratings
            .OfType<UserRating>()
            .Where(r => r.UserId == sourceUserId)
            .ToListAsync(cancellationToken);

        if (sourceRatings.Count == 0) return;

        var targetMediaIds = sourceRatings.Select(r => r.MediaId).ToList();
        var targetRatings = await context.Ratings
            .OfType<UserRating>()
            .Where(r => r.UserId == targetUserId && targetMediaIds.Contains(r.MediaId))
            .ToDictionaryAsync(r => r.MediaId, cancellationToken);

        var strategy = mergeStrategy ?? new MergeStrategy();

        foreach (var sourceRating in sourceRatings)
        {
            if (targetRatings.TryGetValue(sourceRating.MediaId, out var targetRating))
            {
                if (strategy.Rating is RatingConflictMode.Overwrite)
                {
                    targetRating.Value = sourceRating.Value;
                }
            }
            else
            {
                context.Ratings.Add(new UserRating
                {
                    UserId = targetUserId,
                    MediaId = sourceRating.MediaId,
                    Value = sourceRating.Value,
                    MinimumValue = sourceRating.MinimumValue,
                    MaximumValue = sourceRating.MaximumValue
                });
            }

            context.Ratings.Remove(sourceRating);
        }
    }

    private async Task TransferPlaylistsAsync(Guid sourceUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var playlists = await context.Playlists
            .Where(p => p.UserId == sourceUserId)
            .ToListAsync(cancellationToken);

        foreach (var playlist in playlists)
        {
            playlist.UserId = targetUserId;
        }
    }

    private async Task TransferPlaybackSessionsAsync(Guid sourceUserId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var sessions = await context.MediaPlaybackSessions
            .Where(s => s.UserId == sourceUserId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.UserId = targetUserId;
        }
    }
}
