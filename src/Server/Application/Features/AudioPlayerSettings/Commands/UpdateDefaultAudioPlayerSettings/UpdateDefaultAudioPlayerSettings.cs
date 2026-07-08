using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.AudioPlayerSettings.Commands.UpdateDefaultAudioPlayerSettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultAudioPlayerSettingsCommand : IRequest
{
    public required AudioPlayerSettingsDto Settings { get; init; }
}

public class UpdateDefaultAudioPlayerSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultAudioPlayerSettingsCommand>
{
    public async Task Handle(UpdateDefaultAudioPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.AudioPlayerSettings, json, cancellationToken);
    }
}
