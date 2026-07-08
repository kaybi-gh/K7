using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateDefaultAudioPlaybackPolicySettings;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultAudioPlaybackPolicySettingsCommand : IRequest
{
    public required AudioPlaybackPolicySettingsDto Settings { get; init; }
}

public class UpdateDefaultAudioPlaybackPolicySettingsCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultAudioPlaybackPolicySettingsCommand>
{
    public async Task Handle(UpdateDefaultAudioPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.AudioPlaybackPolicy, json, cancellationToken);
    }
}
