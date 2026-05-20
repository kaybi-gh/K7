using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Commands.UpdateUserTrackSelectionPreferences;

[Authorize]
public record UpdateUserTrackSelectionPreferencesCommand : IRequest
{
    public required TrackSelectionPreferencesDto Settings { get; init; }
}

public class UpdateUserTrackSelectionPreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateUserTrackSelectionPreferencesCommand>
{
    public async Task Handle(UpdateUserTrackSelectionPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Settings);
        await userSettingsService.SetAsync(userId, UserSettingKeys.TrackSelectionPreferences, json, cancellationToken);
    }
}
