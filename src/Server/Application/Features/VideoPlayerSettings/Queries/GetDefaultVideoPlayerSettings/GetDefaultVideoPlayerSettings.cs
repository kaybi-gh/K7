using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.VideoPlayerSettings.Queries.GetDefaultVideoPlayerSettings;

[Authorize(Roles = Roles.Administrator)]
public record GetDefaultVideoPlayerSettingsQuery : IRequest<VideoPlayerSettingsDto?>;

public class GetDefaultVideoPlayerSettingsQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultVideoPlayerSettingsQuery, VideoPlayerSettingsDto?>
{
    public async Task<VideoPlayerSettingsDto?> Handle(GetDefaultVideoPlayerSettingsQuery request, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlayerSettings, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<VideoPlayerSettingsDto>(json);

        return null;
    }
}
