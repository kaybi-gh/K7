using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.Shared.Pages;

public partial class Settings
{
    private DeviceType _deviceType;
    private List<MediaFormatDto>? _supportedMediaFormats;
    private bool? _hdrSupport;
    private string? _backendUrl;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
        ThemeService.DarkModeEnabledOnChange += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _deviceType = await DeviceService.GetDeviceTypeAsync();
        _supportedMediaFormats = await DeviceService.GetSupportedMediaFormatsAsync();
        _hdrSupport = await DeviceService.GetHdrSupportAsync();
        _backendUrl = K7ServerService.GetAbsoluteUri()?.AbsoluteUri;
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

    private async Task ChangeBackendUrl()
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            "Warning",
            "Changing K7 server URL will remove all elements related to current K7 server (statistics and files).",
            yesText: "Continue", cancelText: "Cancel");

        if (result == true)
        {
            //K7ServerService.RemoveRegisteredBackendUrl(); // TODO - How do we manage that?
        }
    }
}
