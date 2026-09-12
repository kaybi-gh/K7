using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Scrobbling.Commands.DeleteUserScrobblerAccount;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeleteUserScrobblerAccountCommand(Guid Id) : IRequest;

public class DeleteUserScrobblerAccountCommandHandler(
    IApplicationDbContext context,
    IUser user)
    : IRequestHandler<DeleteUserScrobblerAccountCommand>
{
    public async Task Handle(DeleteUserScrobblerAccountCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.UserScrobblerAccounts
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        context.UserScrobblerAccounts.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
