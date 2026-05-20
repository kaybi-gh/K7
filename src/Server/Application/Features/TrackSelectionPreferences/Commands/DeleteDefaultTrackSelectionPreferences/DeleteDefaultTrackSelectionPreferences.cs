using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.DeleteDefaultTrackSelectionPreferences;

[Authorize(Roles = Roles.Administrator)]
public record DeleteDefaultTrackSelectionPreferencesCommand : IRequest;

public class DeleteDefaultTrackSelectionPreferencesCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<DeleteDefaultTrackSelectionPreferencesCommand>
{
    public async Task Handle(DeleteDefaultTrackSelectionPreferencesCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.RemoveAsync(ServerSettingKeys.TrackSelectionPreferences, cancellationToken);
    }
}
