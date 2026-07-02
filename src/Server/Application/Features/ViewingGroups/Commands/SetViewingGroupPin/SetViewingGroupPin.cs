using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.ViewingGroups.Commands.SetViewingGroupPin;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record SetViewingGroupPinCommand(Guid Id, string? Pin) : IRequest;

public class SetViewingGroupPinCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<SetViewingGroupPinCommand>
{
    public async Task Handle(SetViewingGroupPinCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var group = await ViewingGroupMemberValidator.GetGroupForMemberAsync(
            context, request.Id, currentUser.Id.Value, cancellationToken);

        group.PinHash = request.Pin is null ? null : PinHashHelper.Hash(request.Pin);
        await context.SaveChangesAsync(cancellationToken);
    }
}
