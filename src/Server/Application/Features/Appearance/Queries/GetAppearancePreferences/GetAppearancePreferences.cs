using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;

namespace K7.Server.Application.Features.Appearance.Queries.GetAppearancePreferences;

public record GetAppearancePreferencesQuery : IRequest<AppearancePreferencesDto>;

public class GetAppearancePreferencesQueryHandler(
    IUserSettingsService userSettingsService,
    IUser currentUser)
    : IRequestHandler<GetAppearancePreferencesQuery, AppearancePreferencesDto>
{
    public async Task<AppearancePreferencesDto> Handle(GetAppearancePreferencesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.Appearance, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<AppearancePreferencesDto>(userJson) ?? new AppearancePreferencesDto();
        }

        return new AppearancePreferencesDto();
    }
}
