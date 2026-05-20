using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.UpdateDefaultTrackSelectionPreferences;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultTrackSelectionPreferencesCommand : IRequest
{
    public required TrackSelectionPreferencesDto Settings { get; init; }
}

public class UpdateDefaultTrackSelectionPreferencesCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultTrackSelectionPreferencesCommand>
{
    public async Task Handle(UpdateDefaultTrackSelectionPreferencesCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Settings);
        await serverSettingsService.SetAsync(ServerSettingKeys.TrackSelectionPreferences, json, cancellationToken);
    }
}
