using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Services;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Queries.GetEffectiveAudioPlaybackPolicySettings;

[Authorize]
public record GetEffectiveAudioPlaybackPolicySettingsQuery : IRequest<AudioPlaybackPolicySettingsDto>;

public class GetEffectiveAudioPlaybackPolicySettingsQueryHandler(
    IPlaybackPolicySettingsProvider policyProvider,
    IUser currentUser)
    : IRequestHandler<GetEffectiveAudioPlaybackPolicySettingsQuery, AudioPlaybackPolicySettingsDto>
{
    public Task<AudioPlaybackPolicySettingsDto> Handle(
        GetEffectiveAudioPlaybackPolicySettingsQuery request,
        CancellationToken cancellationToken) =>
        policyProvider.GetEffectiveAudioPolicyAsync(currentUser.Id, cancellationToken);
}
