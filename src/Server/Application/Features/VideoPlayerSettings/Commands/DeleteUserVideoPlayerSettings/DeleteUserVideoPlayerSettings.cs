using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.VideoPlayerSettings.Commands.DeleteUserVideoPlayerSettings;

[Authorize]
public record DeleteUserVideoPlayerSettingsCommand : IRequest;

public class DeleteUserVideoPlayerSettingsCommandHandler(
    IUserSettingsService userSettingsService,
    IUser currentUser,
    ISender sender,
    IUserVideoPlayerSettingsNotifier settingsNotifier)
    : IRequestHandler<DeleteUserVideoPlayerSettingsCommand>
{
    public async Task Handle(DeleteUserVideoPlayerSettingsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.VideoPlayerSettings, cancellationToken);
        var effective = await sender.Send(new GetEffectiveVideoPlayerSettingsQuery(), cancellationToken);

        if (currentUser.IdentityId is { } identityId)
            await settingsNotifier.NotifyVideoPlayerSettingsUpdatedAsync(identityId, effective, cancellationToken);
    }
}
