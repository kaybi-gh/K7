using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using FluentValidation.Results;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Users.Commands.ToggleUserActive;

[Authorize(Roles = Roles.Administrator)]
public record ToggleUserActiveCommand : IRequest
{
    public required Guid Id { get; init; }
    public required bool IsActive { get; init; }
}

public class ToggleUserActiveCommandHandler : IRequestHandler<ToggleUserActiveCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public ToggleUserActiveCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(ToggleUserActiveCommand request, CancellationToken cancellationToken)
    {
        var domainUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, domainUser);

        if (domainUser.IdentityUserId == _user.IdentityId)
            throw new ValidationException(
            [
                new ValidationFailure("Id", "Cannot deactivate your own account.")
            ]);

        domainUser.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
