using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDevicesPanel
{
    [Inject] private IDeviceApiService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorageService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool _isLoading = true;
    private K7.Shared.Dtos.PaginatedListDto<DeviceDto>? _devices;
    private string? _currentDeviceId;
    private Guid? _focusedDeviceId;
    private bool _shouldScrollToFocused;

    private IList<DeviceDto> _deviceItems => _devices?.Items?.ToList() ?? [];

    protected override async Task OnInitializedAsync()
    {
        _currentDeviceId = DeviceStorageService.Get(PreferenceKeys.DEVICE_ID);
        _isLoading = true;
        try
        {
            _devices = await K7ServerService.GetDevicesAsync();
        }
        catch { }
        finally
        {
            _isLoading = false;
        }

        ParseFocusParam();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldScrollToFocused && _focusedDeviceId is not null)
        {
            _shouldScrollToFocused = false;
            await JSRuntime.InvokeVoidAsync("K7.scrollToElement", $"device-{_focusedDeviceId}");
        }
    }

    private void ParseFocusParam()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var query = uri.Query;
        if (!string.IsNullOrEmpty(query))
        {
            var focusParam = query.TrimStart('?').Split('&')
                .Select(p => p.Split('=', 2))
                .FirstOrDefault(p => p.Length == 2 && p[0] == "focus");

            if (focusParam is not null && Guid.TryParse(Uri.UnescapeDataString(focusParam[1]), out var deviceId))
            {
                _focusedDeviceId = deviceId;
                _shouldScrollToFocused = true;
            }
        }
    }

    private bool IsCurrentDevice(DeviceDto device)
    {
        return !string.IsNullOrEmpty(_currentDeviceId)
            && device.Id.ToString().Equals(_currentDeviceId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DeleteDeviceAsync(DeviceDto device)
    {
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteDeviceTitle"],
            L["DeleteDeviceConfirmation"],
            yesText: L["Delete"],
            cancelText: L["Cancel"]);

        if (result is not true)
            return;

        try
        {
            await K7ServerService.DeleteDeviceAsync(device.Id);
            _devices = await K7ServerService.GetDevicesAsync();
        }
        catch { }
    }

    private static string GetDeviceIcon(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Desktop => Phosphor.Desktop,
        DeviceType.Phone => Phosphor.DeviceMobile,
        DeviceType.Tablet => Phosphor.DeviceTablet,
        DeviceType.TV => Phosphor.Television,
        DeviceType.Watch => Phosphor.Watch,
        _ => Phosphor.Devices,
    };

    private static string GetClientTypeIcon(ClientType clientType) => clientType switch
    {
        ClientType.Native => K7Brand.Symbol,
        ClientType.Web => Phosphor.Globe,
        _ => Phosphor.AppWindow,
    };

    private static string GetBrowserIcon(Browser browser) => browser switch
    {
        Browser.Chrome => Phosphor.GoogleChromeLogo,
        Browser.Firefox => Phosphor.Browsers,
        Browser.Edge => Phosphor.Browsers,
        Browser.Safari => Phosphor.AppleLogo,
        Browser.Opera => Phosphor.Browsers,
        _ => Phosphor.Browser,
    };

    private string GetDeviceTypeLabel(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Desktop => L["DeviceTypeDesktop"],
        DeviceType.Phone => L["DeviceTypePhone"],
        DeviceType.Tablet => L["DeviceTypeTablet"],
        DeviceType.TV => L["DeviceTypeTv"],
        DeviceType.Watch => L["DeviceTypeWatch"],
        _ => L["UnknownDevice"]
    };

    private string GetClientTypeLabel(ClientType clientType) => clientType switch
    {
        ClientType.Native => L["ClientTypeNative"],
        ClientType.Web => L["ClientTypeWeb"],
        _ => L["UnknownDevice"]
    };

    private static string GetBrowserLabel(Browser browser) => browser switch
    {
        Browser.Chrome => "Chrome",
        Browser.Firefox => "Firefox",
        Browser.Edge => "Edge",
        Browser.Safari => "Safari",
        Browser.Opera => "Opera",
        _ => "Browser"
    };

    private static string GetDeviceClass(bool isCurrent, bool isFocused)
    {
        return (isCurrent, isFocused) switch
        {
            (true, true) => "current-device device-highlighted",
            (true, false) => "current-device",
            (false, true) => "device-highlighted",
            _ => ""
        };
    }
}
