using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateDefaultVideoPlaybackPolicySettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultVideoPlaybackPolicySettingsCommand : IRequest
{
    public required VideoPlaybackPolicySettingsDto Settings { get; init; }
}

public class UpdateDefaultVideoPlaybackPolicySettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultVideoPlaybackPolicySettingsCommand>
{
    public async Task Handle(UpdateDefaultVideoPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.VideoPlaybackPolicy, json, cancellationToken);
    }
}
