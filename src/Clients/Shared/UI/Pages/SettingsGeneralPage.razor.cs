using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsGeneralPage : IDisposable
{
    private DeviceType _deviceType;
    private List<MediaFormatDto>? _supportedMediaFormats;
    private bool? _hdrSupport;
    private string? _backendUrl;

    protected override void OnInitialized()
    {
        ThemeService.ThemeOnChange += StateHasChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _deviceType = await DeviceService.GetDeviceTypeAsync();
        _supportedMediaFormats = await DeviceService.GetSupportedMediaFormatsAsync();
        _hdrSupport = await DeviceService.GetHdrSupportAsync();
        _backendUrl = ApiClient.HttpClient.BaseAddress?.AbsoluteUri;
    }

    public void Dispose()
    {
        ThemeService.ThemeOnChange -= StateHasChanged;
    }

    private async Task ChangeBackendUrl()
    {
        bool? result = await DialogService.ShowMessageBoxAsync(
            L["WarningTitle"],
            L["ChangeServerUrlWarning"],
            yesText: S["Confirm"], cancelText: S["Cancel"]);

        if (result == true)
        {
            ServerConnectionService.DisconnectAndReset();
        }
    }
}
