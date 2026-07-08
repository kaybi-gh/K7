using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.DeleteDefaultAudioPlaybackPolicySettings;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultAudioPlaybackPolicySettingsCommand : IRequest;

public class DeleteDefaultAudioPlaybackPolicySettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultAudioPlaybackPolicySettingsCommand>
{
    public async Task Handle(DeleteDefaultAudioPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.AudioPlaybackPolicy, cancellationToken);
    }
}
