using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;

namespace K7.Server.Application.Features.SharedProfiles.Commands.VerifySharedProfilePin;

public record VerifySharedProfilePinCommand(Guid SharedProfileId, string Pin) : IRequest<bool>;

public class VerifySharedProfilePinCommandHandler(IApplicationDbContext context)
    : IRequestHandler<VerifySharedProfilePinCommand, bool>
{
    public async Task<bool> Handle(VerifySharedProfilePinCommand request, CancellationToken cancellationToken)
    {
        var group = await context.SharedProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == request.SharedProfileId, cancellationToken);

        if (group is null)
            return false;

        if (group.PinHash is null)
            return true;

        return PinHashHelper.Verify(group.PinHash, request.Pin);
    }
}
