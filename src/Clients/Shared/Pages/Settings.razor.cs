using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.Pages;

public partial class Settings
{
    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
        ThemeService.DarkModeEnabledOnChange += StateHasChanged;
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= StateHasChanged;
        ThemeService.DarkModeEnabledOnChange -= StateHasChanged;
    }

    private void ToggleDrawerVariant()
    {
        ThemeService.ToggleDarkMode();
        StateHasChanged();
    }
}