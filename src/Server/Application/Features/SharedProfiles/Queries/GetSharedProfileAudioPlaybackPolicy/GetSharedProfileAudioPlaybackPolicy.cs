using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileAudioPlaybackPolicy;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetSharedProfileAudioPlaybackPolicyQuery(Guid SharedProfileId) : IRequest<AudioPlaybackPolicySettingsDto>;

public class GetSharedProfileAudioPlaybackPolicyQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IServerSettingsService serverSettingsService)
    : IRequestHandler<GetSharedProfileAudioPlaybackPolicyQuery, AudioPlaybackPolicySettingsDto>
{
    public async Task<AudioPlaybackPolicySettingsDto> Handle(
        GetSharedProfileAudioPlaybackPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForMemberAsync(
            context, request.SharedProfileId, userId, cancellationToken);

        var json = await sharedProfileSettingsService.GetAsync(
            request.SharedProfileId, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(json) ?? new AudioPlaybackPolicySettingsDto();

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.AudioPlaybackPolicy, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<AudioPlaybackPolicySettingsDto>(serverJson) ?? new AudioPlaybackPolicySettingsDto();

        return new AudioPlaybackPolicySettingsDto();
    }
}
