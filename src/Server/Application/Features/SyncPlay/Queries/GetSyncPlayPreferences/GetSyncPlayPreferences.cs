using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SyncPlay.Queries.GetSyncPlayPreferences;

public record GetSyncPlayPreferencesQuery : IRequest<SyncPlayPreferencesDto>;

public class GetSyncPlayPreferencesQueryHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<GetSyncPlayPreferencesQuery, SyncPlayPreferencesDto>
{
    public async Task<SyncPlayPreferencesDto> Handle(GetSyncPlayPreferencesQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = await userSettingsService.GetAsync(userId, UserSettingKeys.SyncPlayPreferences, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<SyncPlayPreferencesDto>(json) ?? new SyncPlayPreferencesDto();

        return new SyncPlayPreferencesDto();
    }
}
