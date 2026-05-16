using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.DeleteUserVideoPlayerSettings;

[Authorize]
public record DeleteUserVideoPlayerSettingsCommand : IRequest;

public class DeleteUserVideoPlayerSettingsCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserVideoPlayerSettingsCommand>
{
    public async Task Handle(DeleteUserVideoPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.VideoPlayerSettings, cancellationToken);
    }
}
