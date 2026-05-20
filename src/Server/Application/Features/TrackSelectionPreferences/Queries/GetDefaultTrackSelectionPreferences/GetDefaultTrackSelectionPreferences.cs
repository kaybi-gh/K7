using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.TrackSelectionPreferences.Queries.GetDefaultTrackSelectionPreferences;

[Authorize(Roles = Roles.Administrator)]
public record GetDefaultTrackSelectionPreferencesQuery : IRequest<TrackSelectionPreferencesDto?>
{
    public Guid? LibraryId { get; init; }
}

public class GetDefaultTrackSelectionPreferencesQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultTrackSelectionPreferencesQuery, TrackSelectionPreferencesDto?>
{
    public async Task<TrackSelectionPreferencesDto?> Handle(GetDefaultTrackSelectionPreferencesQuery request, CancellationToken cancellationToken)
    {
        var key = request.LibraryId is { } libraryId
            ? new SettingKey<string>($"TrackSelectionPreferences:Library:{libraryId}")
            : ServerSettingKeys.TrackSelectionPreferences;
        var json = await serverSettingsService.GetAsync(key, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<TrackSelectionPreferencesDto>(json);

        return null;
    }
}
