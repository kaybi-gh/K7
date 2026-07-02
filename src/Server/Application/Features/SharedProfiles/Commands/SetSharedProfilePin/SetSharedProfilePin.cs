using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.SharedProfiles.Commands.SetSharedProfilePin;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record SetSharedProfilePinCommand(Guid Id, string? Pin) : IRequest;

public class SetSharedProfilePinCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<SetSharedProfilePinCommand>
{
    public async Task Handle(SetSharedProfilePinCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var group = await SharedProfileMemberValidator.GetGroupForMemberAsync(
            context, request.Id, currentUser.Id.Value, cancellationToken);

        group.PinHash = request.Pin is null ? null : PinHashHelper.Hash(request.Pin);
        await context.SaveChangesAsync(cancellationToken);
    }
}
