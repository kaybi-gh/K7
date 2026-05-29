using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SyncPlay.Commands.UpdateSyncPlayPreferences;

[Authorize]
public record UpdateSyncPlayPreferencesCommand : IRequest
{
    public required SyncPlayPreferencesDto Preferences { get; init; }
}

public class UpdateSyncPlayPreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateSyncPlayPreferencesCommand>
{
    public async Task Handle(UpdateSyncPlayPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Preferences);
        await userSettingsService.SetAsync(userId, UserSettingKeys.SyncPlayPreferences, json, cancellationToken);
    }
}
