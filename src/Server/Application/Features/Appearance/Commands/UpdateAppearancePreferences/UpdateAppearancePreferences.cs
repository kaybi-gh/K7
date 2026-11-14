using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;

namespace K7.Server.Application.Features.Appearance.Commands.UpdateAppearancePreferences;

[Authorize]
public record UpdateAppearancePreferencesCommand : IRequest
{
    public required AppearancePreferencesDto Preferences { get; init; }
}

public class UpdateAppearancePreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateAppearancePreferencesCommand>
{
    public async Task Handle(UpdateAppearancePreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Preferences);
        await userSettingsService.SetAsync(userId, UserSettingKeys.Appearance, json, cancellationToken);
    }
}
