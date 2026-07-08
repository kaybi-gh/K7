using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Queries.GetDefaultVideoPlaybackPolicySettings;

[Authorize(Roles = Roles.Administrator)]
public record GetDefaultVideoPlaybackPolicySettingsQuery : IRequest<VideoPlaybackPolicySettingsDto>;

public class GetDefaultVideoPlaybackPolicySettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultVideoPlaybackPolicySettingsQuery, VideoPlaybackPolicySettingsDto>
{
    public async Task<VideoPlaybackPolicySettingsDto> Handle(
        GetDefaultVideoPlaybackPolicySettingsQuery request,
        CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlaybackPolicy, cancellationToken);
        return serverJson is not null
            ? JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(serverJson) ?? new VideoPlaybackPolicySettingsDto()
            : new VideoPlaybackPolicySettingsDto();
    }
}
