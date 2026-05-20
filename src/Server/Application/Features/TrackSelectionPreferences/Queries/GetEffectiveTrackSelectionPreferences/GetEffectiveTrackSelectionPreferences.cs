using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Queries.GetEffectiveTrackSelectionPreferences;

public record GetEffectiveTrackSelectionPreferencesQuery : IRequest<TrackSelectionPreferencesDto>
{
    public Guid? LibraryId { get; init; }
}

public class GetEffectiveTrackSelectionPreferencesQueryHandler(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveTrackSelectionPreferencesQuery, TrackSelectionPreferencesDto>
{
    public async Task<TrackSelectionPreferencesDto> Handle(GetEffectiveTrackSelectionPreferencesQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            // Try user per-library override
            if (request.LibraryId is { } libraryId)
            {
                var userLibraryKey = new SettingKey<string>($"TrackSelectionPreferences:Library:{libraryId}");
                var userLibraryJson = await userSettingsService.GetAsync(userId, userLibraryKey, cancellationToken);
                if (userLibraryJson is not null)
                    return JsonSerializer.Deserialize<TrackSelectionPreferencesDto>(userLibraryJson) ?? new TrackSelectionPreferencesDto();
            }

            // Try user global override
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.TrackSelectionPreferences, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<TrackSelectionPreferencesDto>(userJson) ?? new TrackSelectionPreferencesDto();
        }

        // Try server per-library default
        if (request.LibraryId is { } serverLibraryId)
        {
            var serverLibraryKey = new SettingKey<string>($"TrackSelectionPreferences:Library:{serverLibraryId}");
            var serverLibraryJson = await serverSettingsService.GetAsync(serverLibraryKey, cancellationToken);
            if (serverLibraryJson is not null)
                return JsonSerializer.Deserialize<TrackSelectionPreferencesDto>(serverLibraryJson) ?? new TrackSelectionPreferencesDto();
        }

        // Try server global default
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.TrackSelectionPreferences, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<TrackSelectionPreferencesDto>(serverJson) ?? new TrackSelectionPreferencesDto();

        return new TrackSelectionPreferencesDto();
    }
}
