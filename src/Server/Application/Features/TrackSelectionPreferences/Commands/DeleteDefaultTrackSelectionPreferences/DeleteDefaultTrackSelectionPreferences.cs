using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteDefaultTrackSelectionPreferences;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultTrackSelectionPreferencesCommand : IRequest
{
    public Guid? LibraryId { get; init; }
}

public class DeleteDefaultTrackSelectionPreferencesCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultTrackSelectionPreferencesCommand>
{
    public async Task Handle(DeleteDefaultTrackSelectionPreferencesCommand request, CancellationToken cancellationToken)
    {
        var key = request.LibraryId is { } libraryId
            ? new SettingKey<string>($"TrackSelectionPreferences:Library:{libraryId}")
            : ServerSettingKeys.TrackSelectionPreferences;
        await serverSettingsService.RemoveAsync(key, cancellationToken);
    }
}
