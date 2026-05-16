using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.DeleteDefaultVideoPlayerSettings;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultVideoPlayerSettingsCommand : IRequest;

public class DeleteDefaultVideoPlayerSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultVideoPlayerSettingsCommand>
{
    public async Task Handle(DeleteDefaultVideoPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.VideoPlayerSettings, cancellationToken);
    }
}
