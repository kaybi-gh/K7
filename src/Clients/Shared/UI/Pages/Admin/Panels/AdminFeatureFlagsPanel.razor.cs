using K7.Shared.Dtos;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminFeatureFlagsPanel
{
    private ServerFeatureFlagsDto? _flags;
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _flags = await ServerPreferencesService.GetServerFeatureFlagsAsync();
        }
        catch
        {
            _flags = new ServerFeatureFlagsDto();
        }

        _loading = false;
    }

    private async void UpdateFlag(Func<ServerFeatureFlagsDto, ServerFeatureFlagsDto> update)
    {
        if (_flags is null)
            return;

        _flags = update(_flags);
        StateHasChanged();

        try
        {
            await ServerPreferencesService.UpdateServerFeatureFlagsAsync(_flags);
        }
        catch
        {
            // Revert on failure
            _flags = await ServerPreferencesService.GetServerFeatureFlagsAsync();
            StateHasChanged();
        }
    }
}
