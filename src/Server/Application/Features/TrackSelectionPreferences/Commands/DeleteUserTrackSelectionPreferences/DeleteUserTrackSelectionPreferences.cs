using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteUserTrackSelectionPreferences;

[Authorize]
public record DeleteUserTrackSelectionPreferencesCommand : IRequest;

public class DeleteUserTrackSelectionPreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserTrackSelectionPreferencesCommand>
{
    public async Task Handle(DeleteUserTrackSelectionPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.TrackSelectionPreferences, cancellationToken);
    }
}
