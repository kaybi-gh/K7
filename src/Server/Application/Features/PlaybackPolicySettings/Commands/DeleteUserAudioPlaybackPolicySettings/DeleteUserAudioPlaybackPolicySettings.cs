using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.DeleteUserAudioPlaybackPolicySettings;

[Authorize]
public record DeleteUserAudioPlaybackPolicySettingsCommand : IRequest;

public class DeleteUserAudioPlaybackPolicySettingsCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserAudioPlaybackPolicySettingsCommand>
{
    public async Task Handle(DeleteUserAudioPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
    }
}
