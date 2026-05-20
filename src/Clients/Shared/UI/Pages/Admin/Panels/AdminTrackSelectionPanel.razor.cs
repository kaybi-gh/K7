using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminTrackSelectionPanel
{
    private TrackSelectionPreferencesDto? _preferences;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _preferences = await ServerPreferencesService.GetServerTrackSelectionPreferencesAsync()
                           ?? new TrackSelectionPreferencesDto();
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

        await ServerPreferencesService.UpdateServerTrackSelectionPreferencesAsync(_preferences);
    }

    private async Task ResetAsync()
    {
        await ServerPreferencesService.DeleteServerTrackSelectionPreferencesAsync();
        _preferences = new TrackSelectionPreferencesDto();
    }
}
