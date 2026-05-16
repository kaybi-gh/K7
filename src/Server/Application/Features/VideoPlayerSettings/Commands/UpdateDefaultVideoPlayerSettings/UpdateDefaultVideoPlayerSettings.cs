using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateDefaultVideoPlayerSettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultVideoPlayerSettingsCommand : IRequest
{
    public required VideoPlayerSettingsDto Settings { get; init; }
}

public class UpdateDefaultVideoPlayerSettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultVideoPlayerSettingsCommand>
{
    public async Task Handle(UpdateDefaultVideoPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.VideoPlayerSettings, json, cancellationToken);
    }
}
