using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfilePreferences;

public record GetSharedProfilePreferencesQuery : IRequest<SharedProfilePreferencesDto>;

public class GetSharedProfilePreferencesQueryHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<GetSharedProfilePreferencesQuery, SharedProfilePreferencesDto>
{
    public async Task<SharedProfilePreferencesDto> Handle(GetSharedProfilePreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = await userSettingsService.GetAsync(userId, UserSettingKeys.SharedProfilePreferences, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<SharedProfilePreferencesDto>(json) ?? new SharedProfilePreferencesDto();

        return new SharedProfilePreferencesDto();
    }
}
