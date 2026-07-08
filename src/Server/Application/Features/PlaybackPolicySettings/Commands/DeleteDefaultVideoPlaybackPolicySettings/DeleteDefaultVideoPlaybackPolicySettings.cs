using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.DeleteDefaultVideoPlaybackPolicySettings;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultVideoPlaybackPolicySettingsCommand : IRequest;

public class DeleteDefaultVideoPlaybackPolicySettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultVideoPlaybackPolicySettingsCommand>
{
    public async Task Handle(DeleteDefaultVideoPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.VideoPlaybackPolicy, cancellationToken);
    }
}
