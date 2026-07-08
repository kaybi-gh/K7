using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.AudioPlayerSettings.Commands.DeleteUserAudioPlayerSettings;

[Authorize]
public record DeleteUserAudioPlayerSettingsCommand : IRequest;

public class DeleteUserAudioPlayerSettingsCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserAudioPlayerSettingsCommand>
{
    public async Task Handle(DeleteUserAudioPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return;

        await userSettingsService.RemoveAsync(userId, UserSettingKeys.AudioPlayerSettings, cancellationToken);
    }
}
