using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.GeneralPreferences.Queries.GetEffectiveGeneralPreferences;

public record GetEffectiveGeneralPreferencesQuery : IRequest<GeneralPreferencesDto>;

public class GetEffectiveGeneralPreferencesQueryHandler(
    IUserSettingsService userSettingsService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveGeneralPreferencesQuery, GeneralPreferencesDto>
{
    public async Task<GeneralPreferencesDto> Handle(GetEffectiveGeneralPreferencesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.GeneralPreferences, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<GeneralPreferencesDto>(userJson) ?? new GeneralPreferencesDto();
        }

        return new GeneralPreferencesDto();
    }
}
