using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfile;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeleteSharedProfileCommand(Guid Id) : IRequest;

public class DeleteSharedProfileCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<DeleteSharedProfileCommand>
{
    public async Task Handle(DeleteSharedProfileCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var group = await SharedProfileMemberValidator.GetGroupForHostAsync(
            context,
            identityService,
            request.Id,
            currentUser.Id.Value,
            currentUser.IdentityId,
            cancellationToken);

        var recipientIds = group.Members
            .Select(m => m.UserId)
            .Append(group.HostUserId)
            .Distinct()
            .ToList();

        await SharedProfileMediaStateMigration.MigrateToMembersAsync(
            context, group.Id, recipientIds, cancellationToken);

        context.SharedProfiles.Remove(group);
        await context.SaveChangesAsync(cancellationToken);
    }
}
