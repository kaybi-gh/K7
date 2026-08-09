using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.DeleteUserAudioPlaybackPolicySettings;

[Authorize]
public record DeleteUserAudioPlaybackPolicySettingsCommand : IRequest;

public class DeleteUserAudioPlaybackPolicySettingsCommandHandler(
    IUserSettingsService userSettingsService,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IApplicationDbContext context,
    IIdentityService identityService,
    IUser currentUser)
    : IRequestHandler<DeleteUserAudioPlaybackPolicySettingsCommand>
{
    public async Task Handle(DeleteUserAudioPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var sharedProfileId = await currentUser.GetSharedProfileIdAsync(cancellationToken);

        if (sharedProfileId is { } profileId)
        {
            await SharedProfileMemberValidator.GetGroupForHostAsync(
                context, identityService, profileId, userId, currentUser.IdentityId, cancellationToken);
            await sharedProfileSettingsService.RemoveAsync(
                profileId, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
            return;
        }

        await userSettingsService.RemoveAsync(userId, UserSettingKeys.AudioPlaybackPolicy, cancellationToken);
    }
}
