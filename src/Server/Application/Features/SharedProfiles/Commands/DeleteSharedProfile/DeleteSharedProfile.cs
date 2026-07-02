using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

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

        var group = await context.SharedProfiles
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, group);

        var isMember = group.Members.Any(m => m.UserId == currentUser.Id.Value);
        var isAdmin = !string.IsNullOrEmpty(currentUser.IdentityId)
            && await identityService.IsInRoleAsync(currentUser.IdentityId, Roles.Administrator);

        if (!isMember && !isAdmin)
            throw new ForbiddenAccessException();

        context.SharedProfiles.Remove(group);
        await context.SaveChangesAsync(cancellationToken);
    }
}
