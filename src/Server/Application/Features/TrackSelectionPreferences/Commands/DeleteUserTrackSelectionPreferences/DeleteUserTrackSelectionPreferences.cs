using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteUserTrackSelectionPreferences;

[Authorize]
public record DeleteUserTrackSelectionPreferencesCommand : IRequest
{
    public Guid? LibraryId { get; init; }
}

public class DeleteUserTrackSelectionPreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserTrackSelectionPreferencesCommand>
{
    public async Task Handle(DeleteUserTrackSelectionPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var key = request.LibraryId is { } libraryId
            ? new SettingKey<string>($"TrackSelectionPreferences:Library:{libraryId}")
            : UserSettingKeys.TrackSelectionPreferences;
        await userSettingsService.RemoveAsync(userId, key, cancellationToken);
    }
}
