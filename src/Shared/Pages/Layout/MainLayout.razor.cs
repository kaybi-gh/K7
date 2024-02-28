using MediaClient.Shared.Domain.Enums;
using MediaClient.Shared.Services;

namespace MediaClient.Shared.Pages.Layout;

public partial class MainLayout
{
    private DeviceType _deviceType;

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _deviceType = await CurrentDeviceService.GetDeviceTypeAsync();
            StateHasChanged();
        }
    }
}