using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.ClientAppPasswords.Commands.RevokeClientAppPassword;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RevokeClientAppPasswordCommand(Guid Id) : IRequest;

public class RevokeClientAppPasswordCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<RevokeClientAppPasswordCommand>
{
    public async Task Handle(RevokeClientAppPasswordCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.ClientAppPasswords
            .FirstOrDefaultAsync(
                p => p.Id == request.Id && p.UserId == currentUser.Id!.Value,
                cancellationToken)
            ?? throw new NotFoundException(request.Id.ToString(), nameof(ClientAppPassword));

        var userName = currentUser.IdentityId is not null
            ? await identityService.GetUserNameAsync(currentUser.IdentityId)
            : null;

        entity.AddDomainEvent(new ClientAppPasswordRevokedEvent(
            entity.Id,
            entity.Name,
            entity.UserId,
            userName));

        context.ClientAppPasswords.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
