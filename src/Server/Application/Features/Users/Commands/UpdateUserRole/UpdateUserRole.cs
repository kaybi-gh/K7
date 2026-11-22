using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserRole;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserRoleCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Role { get; init; }
}

public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public UpdateUserRoleCommandHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        var domainUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, domainUser);

        if (domainUser.IdentityUserId is null)
            throw new ValidationException(
            [
                new ValidationFailure("Id", "User has no identity account.")
            ]);

        // Guardians get a full session of the account: it cannot gain admin rights while managed.
        if (request.Role == Roles.Administrator
            && await _context.UserGuardians.AnyAsync(g => g.UserId == request.Id, cancellationToken))
        {
            throw new ValidationException(
            [
                new ValidationFailure("Role", "Remove the guardians of this account before making it an administrator.")
            ]);
        }

        await _identityService.SetRoleAsync(domainUser.IdentityUserId, request.Role);
    }
}
