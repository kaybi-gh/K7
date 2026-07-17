using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.SharedProfiles.Commands.AssignSharedProfileContentRestriction;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record AssignSharedProfileContentRestrictionCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public Guid? ContentRestrictionProfileId { get; init; }
}

public class AssignSharedProfileContentRestrictionCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<AssignSharedProfileContentRestrictionCommand>
{
    public async Task Handle(AssignSharedProfileContentRestrictionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var group = await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        if (request.ContentRestrictionProfileId is { } profileId)
        {
            var exists = await context.ContentRestrictionProfiles
                .AsNoTracking()
                .AnyAsync(p => p.Id == profileId, cancellationToken);
            if (!exists)
                throw new NotFoundException(profileId.ToString(), "ContentRestrictionProfile");
        }

        group.ContentRestrictionProfileId = request.ContentRestrictionProfileId;
        await context.SaveChangesAsync(cancellationToken);
    }
}
