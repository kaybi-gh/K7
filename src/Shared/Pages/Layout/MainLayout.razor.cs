using MediaClient.Shared.Services;
using MudBlazor;

namespace MediaClient.Shared.Pages.Layout;

public partial class MainLayout
{
    private Breakpoint _breakpoint;

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
}