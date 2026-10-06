using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;

public record GetEffectiveVideoPlayerSettingsQuery : IRequest<VideoPlayerSettingsDto>;

public class GetEffectiveVideoPlayerSettingsQueryHandler(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveVideoPlayerSettingsQuery, VideoPlayerSettingsDto>
{
    public async Task<VideoPlayerSettingsDto> Handle(GetEffectiveVideoPlayerSettingsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.VideoPlayerSettings, cancellationToken);
            if (userJson is not null)
                return Normalize(JsonSerializer.Deserialize<VideoPlayerSettingsDto>(userJson));
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlayerSettings, cancellationToken);
        if (serverJson is not null)
            return Normalize(JsonSerializer.Deserialize<VideoPlayerSettingsDto>(serverJson));

        return Normalize(null);
    }

    private static VideoPlayerSettingsDto Normalize(VideoPlayerSettingsDto? settings)
    {
        settings ??= new VideoPlayerSettingsDto();
        settings.RememberTrackSelection ??= true;
        return settings;
    }
}
