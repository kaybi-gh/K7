using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.ClientAppPasswords.Commands.RevokeClientAppPassword;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RevokeClientAppPasswordCommand(Guid Id) : IRequest;

public class RevokeClientAppPasswordCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<RevokeClientAppPasswordCommand>
{
    public async Task Handle(RevokeClientAppPasswordCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.ClientAppPasswords
            .FirstOrDefaultAsync(
                p => p.Id == request.Id && p.UserId == currentUser.Id!.Value,
                cancellationToken)
            ?? throw new NotFoundException(request.Id.ToString(), nameof(ClientAppPassword));

        context.ClientAppPasswords.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
