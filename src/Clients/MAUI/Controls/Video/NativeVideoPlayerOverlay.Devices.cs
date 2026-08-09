using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Cast + remote device picker panel. Mirrors <c>PlayOnDevicePicker.razor(.cs)</c>: Chromecast
/// section via <see cref="ICastService"/>, remote (other logged-in devices) section via
/// <see cref="K7HubClient"/>'s ConnectedDevicesUpdated event, filtered to exclude this device.
/// </summary>
public sealed partial class NativeVideoPlayerOverlay
{
    private readonly Border _castPanel = new();
    private readonly VerticalStackLayout _deviceList = new() { Spacing = 4, Padding = new Thickness(8) };
    private readonly List<CastFocusRow> _castFocusRows = [];
    private int _castFocusIndex = -1;
    private IReadOnlyList<ConnectedDeviceDto> _remoteDevices = [];

    private bool HasAnyCastOrRemoteDevice() =>
        _castService?.IsAvailable == true || _remoteDevices.Count > 0;

    private void BuildDevicePanel()
    {
        _castPanel.BackgroundColor = Color.FromArgb("#F2121212");
        _castPanel.Stroke = Color.FromArgb("#33FFFFFF");
        _castPanel.StrokeThickness = 1;
        _castPanel.StrokeShape = new RoundRectangle { CornerRadius = 10 };
        _castPanel.Padding = new Thickness(4);
        _castPanel.WidthRequest = 320;
        _castPanel.MaximumHeightRequest = 360;
        _castPanel.IsVisible = false;
        _castPanel.HorizontalOptions = LayoutOptions.End;
        _castPanel.VerticalOptions = LayoutOptions.End;
        _castPanel.Margin = new Thickness(16, 16, 16, 88);
        _castPanel.Content = new ScrollView { Content = _deviceList };
        Children.Add(_castPanel);

        if (_castService is not null)
            _castService.DevicesDiscovered += OnCastDevicesDiscovered;

        if (_hubClient is not null)
            _hubClient.ConnectedDevicesUpdated += OnConnectedDevicesUpdated;
    }

    private void ToggleCastPanel()
    {
        SetCastPanelOpen(!_castPanelOpen);
        if (_castPanelOpen)
        {
            _ = StartCastDiscoveryAsync();
            _ = _hubClient?.RequestConnectedDevicesAsync();
        }
    }

    private void SetCastPanelOpen(bool open)
    {
        _castPanelOpen = open;
        _castPanel.IsVisible = open;
        if (open)
        {
            _settings.Close();
            SetSyncPlayPanelOpen(false);
            StopHideTimer();
            RebuildDeviceList();
        }
        else
        {
            _castFocusIndex = -1;
            ResetHideTimer();
        }

        UpdateChromeVisibility();
    }

    private async Task StartCastDiscoveryAsync()
    {
        if (_castService is null)
            return;

        try
        {
            await _castService.StartDiscoveryAsync();
            RebuildDeviceList();
        }
        catch
        {
            // Best-effort discovery.
        }
    }

    private void OnCastDevicesDiscovered(IReadOnlyList<CastDeviceInfo> devices) =>
        MainThread.BeginInvokeOnMainThread(RebuildDeviceList);

    private void OnConnectedDevicesUpdated(IReadOnlyList<ConnectedDeviceDto> devices) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var currentDeviceId = _deviceStorage?.Get(PreferenceKeys.DEVICE_ID);
            _remoteDevices = Guid.TryParse(currentDeviceId, out var selfId)
                ? devices.Where(d => d.DeviceId != selfId).ToList()
                : devices;
            _castButton.IsVisible = HasAnyCastOrRemoteDevice();
            RebuildDeviceList();
        });

    private void RebuildDeviceList()
    {
        _deviceList.Children.Clear();
        _castFocusRows.Clear();
        _castFocusIndex = -1;

        var castDevices = _castService?.DiscoveredDevices ?? [];
        if (_castService?.IsAvailable == true)
        {
            _deviceList.Children.Add(CreateSectionHeader(NativeStrings.Chromecast));
            if (castDevices.Count > 0)
            {
                foreach (var device in castDevices)
                    AddCastFocusRow(NativePlayerGlyphs.Television, device.Name, () => _ = OnCastDeviceRowClicked(device));
            }
            else
            {
                AddCastFocusRow(
                    NativePlayerGlyphs.Television,
                    NativeStrings.CastToDevice,
                    () => _ = OnCastDeviceRowClicked(new CastDeviceInfo("default", "Chromecast")));
            }
        }

        if (_remoteDevices.Count > 0)
        {
            _deviceList.Children.Add(CreateSectionHeader(NativeStrings.RemoteDevices));
            foreach (var device in _remoteDevices)
                AddCastFocusRow(GetDeviceIcon(device.DeviceType), device.DeviceName, () => _ = OnRemoteDeviceRowClicked(device));
        }

        if (_castFocusRows.Count == 0)
        {
            _deviceList.Children.Add(new Label
            {
                Text = NativeStrings.SearchingForDevices,
                TextColor = Colors.White,
                FontSize = 14,
                Padding = new Thickness(12, 10)
            });
            return;
        }

        SetCastFocusIndex(0);
    }

    private void AddCastFocusRow(string icon, string text, Action onClick)
    {
        var view = NativeIconText.CreateTappableRow(icon, text, selected: false, onClick);
        var row = new CastFocusRow(view, onClick);
        _castFocusRows.Add(row);
        _deviceList.Children.Add(view);
    }

    private bool MoveCastFocus(int direction)
    {
        if (!_castPanelOpen || _castFocusRows.Count == 0)
            return false;

        var next = NativeSettingsFocusNavigator.MoveFocus(_castFocusIndex, _castFocusRows.Count, direction);
        SetCastFocusIndex(next);
        return true;
    }

    private bool ActivateCastFocus()
    {
        if (!_castPanelOpen || _castFocusIndex < 0 || _castFocusIndex >= _castFocusRows.Count)
            return false;

        _castFocusRows[_castFocusIndex].Activate();
        return true;
    }

    private void SetCastFocusIndex(int index)
    {
        if (_castFocusIndex >= 0 && _castFocusIndex < _castFocusRows.Count)
            ApplyCastRowVisual(_castFocusRows[_castFocusIndex], focused: false);

        _castFocusIndex = NativeSettingsFocusNavigator.ClampFocus(index, _castFocusRows.Count);

        if (_castFocusIndex >= 0 && _castFocusIndex < _castFocusRows.Count)
            ApplyCastRowVisual(_castFocusRows[_castFocusIndex], focused: true);
    }

    private static void ApplyCastRowVisual(CastFocusRow row, bool focused)
    {
        row.View.BackgroundColor = focused ? Color.FromArgb("#66FFFFFF") : Colors.Transparent;
        row.View.Stroke = focused ? Colors.White : Colors.Transparent;
        row.View.StrokeThickness = focused ? 2 : 0;
    }

    private static Label CreateSectionHeader(string text) => new()
    {
        Text = text,
        TextColor = Color.FromArgb("#99FFFFFF"),
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        Padding = new Thickness(12, 8, 12, 4)
    };

    private static string GetDeviceIcon(string deviceType) => deviceType switch
    {
        "Desktop" => NativePlayerGlyphs.Desktop,
        "Mobile" => NativePlayerGlyphs.DeviceMobile,
        "TV" => NativePlayerGlyphs.Television,
        _ => NativePlayerGlyphs.Monitor
    };

    private async Task OnCastDeviceRowClicked(CastDeviceInfo device)
    {
        if (_castOrchestration is not null)
            await _castOrchestration.CastCurrentVideoAsync(device);
        SetCastPanelOpen(false);
    }

    /// <summary>Mirrors VideoPlayerControlsOverlay.OnRemoteDeviceSelected: pause locally, hand
    /// off playback to the target device via the hub, then track the remote session.</summary>
    private async Task OnRemoteDeviceRowClicked(ConnectedDeviceDto device)
    {
        SetCastPanelOpen(false);

        var source = _player.Source;
        if (source?.IndexedFileId is null || _hubClient is null)
            return;

        _player.Pause();

        var senderDeviceId = _deviceStorage?.Get(PreferenceKeys.DEVICE_ID);
        var request = new RemotePlaybackRequestDto
        {
            IndexedFileId = source.IndexedFileId.Value,
            StartPosition = _player.CurrentTime,
            IsAudio = false,
            Title = source.Title,
            CoverUrl = source.CoverUrl,
            Duration = _player.Duration,
            SenderDeviceId = senderDeviceId is not null ? Guid.Parse(senderDeviceId.AsSpan()) : null
        };

        await _hubClient.RequestRemotePlaybackAsync(device.DeviceId, request);
        _remoteControl?.StartSession(device.DeviceId, device.DeviceName, request);
    }

    private sealed class CastFocusRow(Border view, Action activate)
    {
        public Border View { get; } = view;
        public void Activate() => activate();
    }
}
