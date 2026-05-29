using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class PlayOnDevicePicker : ComponentBase, IDisposable
{
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;

    [Parameter] public string? ButtonClass { get; set; }
    [Parameter] public EventCallback<CastDeviceInfo> CastDeviceSelected { get; set; }
    [Parameter] public EventCallback<ConnectedDeviceDto> RemoteDeviceSelected { get; set; }

    private bool _isOpen;
    private IReadOnlyList<ConnectedDeviceDto> _remoteDevices = [];

    private bool _hasAnyDevice =>
        CastService.IsAvailable || _remoteDevices.Count > 0;

    protected override void OnInitialized()
    {
        CastService.StateChanged += OnCastStateChanged;
        CastService.DevicesDiscovered += OnDevicesDiscovered;
        HubClient.ConnectedDevicesUpdated += OnConnectedDevicesUpdated;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await CastService.StartDiscoveryAsync();
            await HubClient.RequestConnectedDevicesAsync();
        }
    }

    private void OnCastStateChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnDevicesDiscovered(IReadOnlyList<CastDeviceInfo> _)
    {
        InvokeAsync(StateHasChanged);
    }

    private void OnConnectedDevicesUpdated(IReadOnlyList<ConnectedDeviceDto> devices)
    {
        var currentDeviceId = DeviceStorage.Get(PreferenceKeys.DEVICE_ID);
        _remoteDevices = Guid.TryParse(currentDeviceId, out var selfId)
            ? devices.Where(d => d.DeviceId != selfId).ToList()
            : devices;
        InvokeAsync(StateHasChanged);
    }

    private async Task OnCastDeviceSelected(CastDeviceInfo device)
    {
        _isOpen = false;
        await CastDeviceSelected.InvokeAsync(device);
    }

    private async Task OnRemoteDeviceSelected(ConnectedDeviceDto device)
    {
        _isOpen = false;
        await RemoteDeviceSelected.InvokeAsync(device);
    }

    private static string GetDeviceIcon(string deviceType) => deviceType switch
    {
        "Desktop" => Phosphor.Desktop,
        "Mobile" => Phosphor.DeviceMobile,
        "TV" => Phosphor.Television,
        _ => Phosphor.Monitor
    };

    public void Dispose()
    {
        CastService.StateChanged -= OnCastStateChanged;
        CastService.DevicesDiscovered -= OnDevicesDiscovered;
        HubClient.ConnectedDevicesUpdated -= OnConnectedDevicesUpdated;
    }
}
