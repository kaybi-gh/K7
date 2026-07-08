using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Queries.GetDefaultAudioPlaybackPolicySettings;

[Authorize(Roles = Roles.Administrator)]
public record GetDefaultAudioPlaybackPolicySettingsQuery : IRequest<AudioPlaybackPolicySettingsDto>;

public class GetDefaultAudioPlaybackPolicySettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultAudioPlaybackPolicySettingsQuery, AudioPlaybackPolicySettingsDto>
{
    public async Task<AudioPlaybackPolicySettingsDto> Handle(
        GetDefaultAudioPlaybackPolicySettingsQuery request,
        CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.AudioPlaybackPolicy, cancellationToken);
        return serverJson is not null
            ? JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(serverJson) ?? new AudioPlaybackPolicySettingsDto()
            : new AudioPlaybackPolicySettingsDto();
    }
}
