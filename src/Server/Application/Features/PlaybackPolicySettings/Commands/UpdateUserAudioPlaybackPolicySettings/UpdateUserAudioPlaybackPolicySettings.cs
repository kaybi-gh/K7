using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.UpdateUserAudioPlaybackPolicySettings;

[Authorize]
public record UpdateUserAudioPlaybackPolicySettingsCommand : IRequest
{
    public required AudioPlaybackPolicySettingsDto Settings { get; init; }
}

public class UpdateUserAudioPlaybackPolicySettingsCommandHandler(
    IUserSettingsService userSettingsService,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IApplicationDbContext context,
    IIdentityService identityService,
    IUser currentUser)
    : IRequestHandler<UpdateUserAudioPlaybackPolicySettingsCommand>
{
    public async Task Handle(UpdateUserAudioPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Settings);
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);

        if (sharedProfileId is { } profileId)
        {
            await SharedProfileMemberValidator.GetGroupForHostAsync(
                context, identityService, profileId, userId, currentUser.IdentityId, cancellationToken);
            await sharedProfileSettingsService.SetAsync(
                profileId, UserSettingKeys.AudioPlaybackPolicy, json, cancellationToken);
            return;
        }

        await userSettingsService.SetAsync(userId, UserSettingKeys.AudioPlaybackPolicy, json, cancellationToken);
    }
}
