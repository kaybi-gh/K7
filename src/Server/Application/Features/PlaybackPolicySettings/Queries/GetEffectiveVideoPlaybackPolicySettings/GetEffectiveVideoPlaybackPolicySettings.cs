using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Services;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Queries.GetEffectiveVideoPlaybackPolicySettings;

[Authorize]
public record GetEffectiveVideoPlaybackPolicySettingsQuery : IRequest<VideoPlaybackPolicySettingsDto>;

public class GetEffectiveVideoPlaybackPolicySettingsQueryHandler(
    IPlaybackPolicySettingsProvider policyProvider,
    IUser currentUser)
    : IRequestHandler<GetEffectiveVideoPlaybackPolicySettingsQuery, VideoPlaybackPolicySettingsDto>
{
    public async Task<VideoPlaybackPolicySettingsDto> Handle(
        GetEffectiveVideoPlaybackPolicySettingsQuery request,
        CancellationToken cancellationToken)
    {
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);
        return await policyProvider.GetEffectiveVideoPolicyAsync(
            currentUser.Id,
            sharedProfileId,
            cancellationToken);
    }
}
