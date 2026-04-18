using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminDevicesPanel
{
    [Inject] private IDeviceApiService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorageService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    private bool _isLoading = true;
    private K7.Shared.Dtos.PaginatedListDto<DeviceDto>? _devices;
    private string? _currentDeviceId;

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
        DeviceType.Desktop => "desktop",
        DeviceType.Phone => "device-mobile",
        DeviceType.Tablet => "device-tablet",
        DeviceType.TV => "television",
        DeviceType.Watch => "watch",
        _ => "devices"
    };
}
