using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.DeleteUser;

[Authorize(Roles = Roles.Administrator)]
public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public DeleteUserCommandHandler(IApplicationDbContext context, IIdentityService identityService, IUser user)
    {
        _context = context;
        _identityService = identityService;
        _user = user;
    }

    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var domainUser = await _context.Users
            .Include(u => u.CapabilityOverrides)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, domainUser);

        if (domainUser.IdentityUserId == _user.IdentityId)
            throw new ValidationException(
            [
                new ValidationFailure("Id", "Cannot delete your own account.")
            ]);

        if (domainUser.IdentityUserId is not null)
        {
            var roles = await _identityService.GetRolesAsync(domainUser.IdentityUserId);
            if (roles.Contains(Roles.Guest))
                throw new ValidationException(
                [
                    new ValidationFailure("Id", "Cannot delete the guest account. Disable guest mode instead.")
                ]);

        }

        var identityUserId = domainUser.IdentityUserId;

        _context.Users.Remove(domainUser);
        await _context.SaveChangesAsync(cancellationToken);

        if (identityUserId is not null)
            await _identityService.DeleteUserAsync(identityUserId);
    }
}
