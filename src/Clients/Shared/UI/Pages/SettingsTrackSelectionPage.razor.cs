using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsTrackSelectionPage
{
    private TrackSelectionPreferencesDto? _preferences;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
        }
        catch
        {
            _preferences = new TrackSelectionPreferencesDto();
        }

        _loading = false;
    }

    private async Task SaveAsync()
    {
        if (_preferences is null)
            return;

        await UserPreferencesService.UpdateUserTrackSelectionPreferencesAsync(_preferences);
    }

    private async Task ResetAsync()
    {
        await UserPreferencesService.ResetUserTrackSelectionPreferencesAsync();
        _preferences = await UserPreferencesService.GetEffectiveTrackSelectionPreferencesAsync();
    }
}
