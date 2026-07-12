using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;

namespace K7.Server.Application.Features.Users.Commands.VerifyUserPin;

public record VerifyUserPinCommand(Guid UserId, string Pin) : IRequest<bool>;

public class VerifyUserPinCommandHandler(IApplicationDbContext context) : IRequestHandler<VerifyUserPinCommand, bool>
{
    public async Task<bool> Handle(VerifyUserPinCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user?.PinHash is null)
            return true;

        return PinHashHelper.Verify(user.PinHash, request.Pin);
    }
}
