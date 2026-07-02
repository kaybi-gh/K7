using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfilePreferences;

[Authorize]
public record UpdateSharedProfilePreferencesCommand : IRequest
{
    public required SharedProfilePreferencesDto Preferences { get; init; }
}

public class UpdateSharedProfilePreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateSharedProfilePreferencesCommand>
{
    public async Task Handle(UpdateSharedProfilePreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Preferences);
        await userSettingsService.SetAsync(userId, UserSettingKeys.SharedProfilePreferences, json, cancellationToken);
    }
}
