using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.Home.Commands.DeleteUserHomeLayout;

[Authorize]
public record DeleteUserHomeLayoutCommand : IRequest;

public class DeleteUserHomeLayoutCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserHomeLayoutCommand>
{
    public async Task Handle(DeleteUserHomeLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.HomeLayout, cancellationToken);
    }
}
