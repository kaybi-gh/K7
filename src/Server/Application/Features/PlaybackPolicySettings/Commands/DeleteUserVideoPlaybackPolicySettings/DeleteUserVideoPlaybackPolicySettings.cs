using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.PlaybackPolicySettings.Commands.DeleteUserVideoPlaybackPolicySettings;

[Authorize]
public record DeleteUserVideoPlaybackPolicySettingsCommand : IRequest;

public class DeleteUserVideoPlaybackPolicySettingsCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserVideoPlaybackPolicySettingsCommand>
{
    public async Task Handle(DeleteUserVideoPlaybackPolicySettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.VideoPlaybackPolicy, cancellationToken);
    }
}
