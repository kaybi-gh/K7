using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Users.Commands.ToggleMediaExclusion;

public record ToggleMediaExclusionCommand : IRequest<bool>, IMediaScopedRequest, IAllowsExcludedMediaAccess
{
    public required Guid MediaId { get; init; }
}

public class ToggleMediaExclusionCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaQueryCacheInvalidator cacheInvalidator,
    IIdentityService identityService)
    : IRequestHandler<ToggleMediaExclusionCommand, bool>
{
    public async Task<bool> Handle(ToggleMediaExclusionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var userName = currentUser.IdentityId is not null
            ? await identityService.GetUserNameAsync(currentUser.IdentityId)
            : null;

        var existing = await context.UserMediaExclusions
            .FirstOrDefaultAsync(e => e.UserId == userId && e.MediaId == request.MediaId, cancellationToken);

        if (existing?.IsAdminExcluded == true)
        {
            var isAdmin = currentUser.IdentityId is not null
                && await identityService.IsInRoleAsync(currentUser.IdentityId, Roles.Administrator);
            if (!isAdmin)
                throw new ForbiddenAccessException();

            existing.IsAdminExcluded = false;
            existing.IsSelfExcluded = false;
            AddHiddenChangedEvent(existing, userId, userName, request.MediaId);
            context.UserMediaExclusions.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            cacheInvalidator.InvalidateAll();
            return false;
        }

        var isSelfExcludedAfterToggle = true;
        if (existing is not null)
        {
            existing.IsSelfExcluded = !existing.IsSelfExcluded;
            isSelfExcludedAfterToggle = existing.IsSelfExcluded;
            AddHiddenChangedEvent(existing, userId, userName, request.MediaId);

            if (!existing.IsAdminExcluded && !existing.IsSelfExcluded)
                context.UserMediaExclusions.Remove(existing);
        }
        else
        {
            var exclusion = new UserMediaExclusion
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MediaId = request.MediaId,
                IsSelfExcluded = true
            };
            AddHiddenChangedEvent(exclusion, userId, userName, request.MediaId);
            context.UserMediaExclusions.Add(exclusion);
        }

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();
        return isSelfExcludedAfterToggle;
    }

    private static void AddHiddenChangedEvent(
        UserMediaExclusion exclusion,
        Guid userId,
        string? userName,
        Guid mediaId)
    {
        var isHidden = exclusion.IsSelfExcluded || exclusion.IsAdminExcluded;
        exclusion.AddDomainEvent(new MediaHiddenChangedEvent(
            userId,
            userName,
            mediaId,
            isHidden,
            exclusion.IsSelfExcluded,
            exclusion.IsAdminExcluded));
    }
}
