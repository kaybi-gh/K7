using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.ViewingGroups.Commands.DeleteViewingGroup;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeleteViewingGroupCommand(Guid Id) : IRequest;

public class DeleteViewingGroupCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<DeleteViewingGroupCommand>
{
    public async Task Handle(DeleteViewingGroupCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var group = await context.ViewingGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, group);

        var isMember = group.Members.Any(m => m.UserId == currentUser.Id.Value);
        var isAdmin = !string.IsNullOrEmpty(currentUser.IdentityId)
            && await identityService.IsInRoleAsync(currentUser.IdentityId, Roles.Administrator);

        if (!isMember && !isAdmin)
            throw new ForbiddenAccessException();

        context.ViewingGroups.Remove(group);
        await context.SaveChangesAsync(cancellationToken);
    }
}
