using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Devices;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDevicesPanel : IAsyncDisposable
{
    [Inject] private IDeviceApiService K7ServerService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorageService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private const int PageSize = 100;

    private bool _isLoading = true;
    private K7.Shared.Dtos.PaginatedListDto<DeviceDto>? _devices;
    private string? _currentDeviceId;
    private Guid? _focusedDeviceId;
    private bool _shouldScrollToFocused;
    private bool _selectionMode;
    private bool _deleting;
    private readonly HashSet<Guid> _selectedIds = [];
    private List<DeviceDto> _deviceItems = [];
    private SelectionModeKeyboardBinder? _selectionKeys;

    private int SelectedCount => _selectedIds.Count;
    private bool AllSelected => _deviceItems.Count > 0 && _selectedIds.Count == _deviceItems.Count;

    protected override async Task OnInitializedAsync()
    {
        _selectionKeys = new SelectionModeKeyboardBinder(
            SpatialNav,
            onEscape: () => _ = InvokeAsync(OnSelectionEscape),
            onSelectAll: () => _ = InvokeAsync(OnSelectionSelectAll));

        _currentDeviceId = DeviceStorageService.Get(PreferenceKeys.DEVICE_ID);
        await LoadDevicesAsync();
        ParseFocusParam();
    }

    public async ValueTask DisposeAsync()
    {
        if (_selectionKeys is not null)
            await _selectionKeys.DisposeAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_shouldScrollToFocused && _focusedDeviceId is not null)
        {
            _shouldScrollToFocused = false;
            await JSRuntime.InvokeVoidAsync("K7.scrollToElement", $"device-{_focusedDeviceId}");
        }
    }

    private async Task LoadDevicesAsync()
    {
        _isLoading = true;
        try
        {
            var items = new List<DeviceDto>();
            var page = 1;
            int? totalCount = null;

            while (true)
            {
                var latest = await K7ServerService.GetDevicesAsync(new GetDevicesQuery
                {
                    PageNumber = page,
                    PageSize = PageSize
                });

                if (latest?.Items is not { Count: > 0 })
                    break;

                totalCount ??= latest.TotalCount;
                items.AddRange(latest.Items);

                if (latest.Items.Count < PageSize)
                    break;

                if (totalCount is int knownTotal && items.Count >= knownTotal)
                    break;

                page++;
            }

            _devices = new PaginatedListDto<DeviceDto>
            {
                Items = items,
                PageNumber = 1,
                TotalPages = 1,
                TotalCount = totalCount ?? items.Count
            };
            _deviceItems = items;
        }
        catch
        {
            _devices = null;
            _deviceItems = [];
        }
        finally
        {
            _isLoading = false;
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

    private void EnterSelectionMode()
    {
        _selectionMode = true;
        _selectedIds.Clear();
        _ = _selectionKeys?.SetEnabledAsync(true);
    }

    private void ExitSelectionMode()
    {
        _selectionMode = false;
        _selectedIds.Clear();
        _ = _selectionKeys?.SetEnabledAsync(false);
    }

    private void ToggleSelection(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);
    }

    private void ToggleSelectAll()
    {
        if (AllSelected)
        {
            _selectedIds.Clear();
            return;
        }

        SelectAll();
    }

    private void SelectAll()
    {
        _selectedIds.Clear();
        foreach (var device in _deviceItems)
            _selectedIds.Add(device.Id);
    }

    private void OnSelectionEscape()
    {
        if (_deleting)
            return;

        ExitSelectionMode();
    }

    private void OnSelectionSelectAll()
    {
        if (!_selectionMode || _deleting)
            return;

        SelectAll();
    }

    private bool IsSelected(Guid id) => _selectedIds.Contains(id);

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
            await LoadDevicesAsync();
        }
        catch { }
    }

    private async Task ConfirmDeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || _deleting)
            return;

        var count = _selectedIds.Count;
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteSelectedTitle"],
            string.Format(L["DeleteSelectedMessage"], count),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (result != true)
            return;

        _deleting = true;
        var failed = 0;

        try
        {
            foreach (var id in _selectedIds.ToList())
            {
                try
                {
                    await K7ServerService.DeleteDeviceAsync(id);
                }
                catch
                {
                    failed++;
                }
            }
        }
        finally
        {
            _deleting = false;
        }

        ExitSelectionMode();
        await LoadDevicesAsync();

        if (failed == 0)
            Snackbar.Add(string.Format(L["DeleteSelectedSuccess"], count), K7Severity.Success);
        else if (failed == count)
            Snackbar.Add(L["DeleteSelectedError"], K7Severity.Error);
        else
            Snackbar.Add(string.Format(L["DeleteSelectedPartial"], count - failed, failed), K7Severity.Warning);
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
        ClientType.External => Phosphor.PlugsConnected,
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
        ClientType.External => L["ClientTypeExternal"],
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
