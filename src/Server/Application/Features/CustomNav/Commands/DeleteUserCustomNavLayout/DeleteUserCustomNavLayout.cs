using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.CustomNav.Queries.GetEffectiveCustomNavLayout;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.CustomNav.Commands.DeleteUserCustomNavLayout;

[Authorize]
public record DeleteUserCustomNavLayoutCommand : IRequest;

public class DeleteUserCustomNavLayoutCommandHandler(
    IUserSettingsService userSettingsService,
    IUser currentUser,
    ISender sender,
    IUserCustomNavNotifier customNavNotifier)
    : IRequestHandler<DeleteUserCustomNavLayoutCommand>
{
    public async Task Handle(DeleteUserCustomNavLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.CustomNavLayout, cancellationToken);

        if (currentUser.IdentityId is not { } identityId)
            return;

        var effective = await sender.Send(new GetEffectiveCustomNavLayoutQuery(), cancellationToken);
        await customNavNotifier.NotifyCustomNavLayoutUpdatedAsync(identityId, effective, cancellationToken);
    }
}
