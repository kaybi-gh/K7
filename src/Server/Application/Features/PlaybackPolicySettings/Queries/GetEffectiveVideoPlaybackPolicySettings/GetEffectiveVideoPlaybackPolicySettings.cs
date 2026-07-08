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
    public Task<VideoPlaybackPolicySettingsDto> Handle(
        GetEffectiveVideoPlaybackPolicySettingsQuery request,
        CancellationToken cancellationToken) =>
        policyProvider.GetEffectiveVideoPolicyAsync(currentUser.Id, cancellationToken);
}
