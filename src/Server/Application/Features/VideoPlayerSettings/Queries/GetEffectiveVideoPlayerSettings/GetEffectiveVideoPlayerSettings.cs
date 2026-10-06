using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.LastVideoTrackSelection;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;

public record GetEffectiveVideoPlayerSettingsQuery : IRequest<VideoPlayerSettingsDto>;

public class GetEffectiveVideoPlayerSettingsQueryHandler(
    IApplicationDbContext context,
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveVideoPlayerSettingsQuery, VideoPlayerSettingsDto>
{
    public async Task<VideoPlayerSettingsDto> Handle(GetEffectiveVideoPlayerSettingsQuery request, CancellationToken cancellationToken)
    {
        VideoPlayerSettingsDto? settings = null;

        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.VideoPlayerSettings, cancellationToken);
            if (userJson is not null)
                settings = JsonSerializer.Deserialize<VideoPlayerSettingsDto>(userJson);
        }

        if (settings is null)
        {
            var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlayerSettings, cancellationToken);
            if (serverJson is not null)
                settings = JsonSerializer.Deserialize<VideoPlayerSettingsDto>(serverJson);
        }

        settings = Normalize(settings);

        // Shared-profile track memory follows the host's RememberTrackSelection toggle.
        if (await currentUser.GetSharedProfileIdAsync(cancellationToken) is not null)
        {
            settings.RememberTrackSelection = await LastVideoTrackSelectionScope.IsRememberEnabledAsync(
                context, userSettingsService, serverSettingsService, currentUser, cancellationToken);
        }

        return settings;
    }

    private static VideoPlayerSettingsDto Normalize(VideoPlayerSettingsDto? settings)
    {
        settings ??= new VideoPlayerSettingsDto();
        settings.RememberTrackSelection ??= true;
        return settings;
    }
}
