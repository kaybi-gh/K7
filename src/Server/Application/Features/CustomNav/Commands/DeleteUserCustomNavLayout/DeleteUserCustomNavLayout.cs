using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.CustomNav.Commands.DeleteUserCustomNavLayout;

[Authorize]
public record DeleteUserCustomNavLayoutCommand : IRequest;

public class DeleteUserCustomNavLayoutCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserCustomNavLayoutCommand>
{
    public async Task Handle(DeleteUserCustomNavLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.CustomNavLayout, cancellationToken);
    }
}
