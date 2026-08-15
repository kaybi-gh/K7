using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.GeneralPreferences.Commands.UpdateUserGeneralPreferences;

[Authorize]
public record UpdateUserGeneralPreferencesCommand : IRequest
{
    public required GeneralPreferencesDto Settings { get; init; }
}

public class UpdateUserGeneralPreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateUserGeneralPreferencesCommand>
{
    public async Task Handle(UpdateUserGeneralPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Settings);
        await userSettingsService.SetAsync(userId, UserSettingKeys.GeneralPreferences, json, cancellationToken);
    }
}
