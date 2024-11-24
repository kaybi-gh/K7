using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.Pages;

public partial class Settings
{
    private List<string>? _supportedCodecs;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
        ThemeService.DarkModeEnabledOnChange += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _supportedCodecs = await DeviceService.GetSupportedCodecsAsync();
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
