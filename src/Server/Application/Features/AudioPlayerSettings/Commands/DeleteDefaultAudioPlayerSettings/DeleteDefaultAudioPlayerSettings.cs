using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.AudioPlayerSettings.Commands.DeleteDefaultAudioPlayerSettings;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultAudioPlayerSettingsCommand : IRequest;

public class DeleteDefaultAudioPlayerSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultAudioPlayerSettingsCommand>
{
    public async Task Handle(DeleteDefaultAudioPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.AudioPlayerSettings, cancellationToken);
    }
}
