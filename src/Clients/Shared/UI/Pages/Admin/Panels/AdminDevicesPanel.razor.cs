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
    [Inject] private IUserAdminService UserService { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorageService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private const string BasePath = "/admin/devices";
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
    private string _search = "";
    private Dictionary<Guid, string> _userNames = [];
    private SelectionModeKeyboardBinder? _selectionKeys;

    /// <summary>Device whose sheet is open. Null shows the list (plus a default sheet on wide screens).</summary>
    [Parameter] public Guid? DeviceId { get; set; }

    private List<DeviceDto> FilteredDevices
    {
        get
        {
            var search = _search.Trim();
            if (search.Length == 0)
                return _deviceItems;

            return _deviceItems
                .Where(d => GetDeviceName(d).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || GetDeviceTypeLabel(d.DeviceType).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || GetDeviceSummary(d).Contains(search, StringComparison.OrdinalIgnoreCase)
                    || GetUserNames(d).Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>The device from the address, else the one a link pointed at, else the first of the list.</summary>
    private DeviceDto? SelectedDevice =>
        _deviceItems.FirstOrDefault(d => d.Id == DeviceId)
        ?? _deviceItems.FirstOrDefault(d => d.Id == _focusedDeviceId)
        ?? FilteredDevices.FirstOrDefault();

    private bool IsDeviceOpen => DeviceId is not null && _deviceItems.Any(d => d.Id == DeviceId);

    private int SelectedCount => _selectedIds.Count;
    private bool AllSelected => _deviceItems.Count > 0 && _selectedIds.Count == _deviceItems.Count;

    protected override async Task OnInitializedAsync()
    {
        _selectionKeys = new SelectionModeKeyboardBinder(
            SpatialNav,
            onEscape: () => _ = InvokeAsync(OnSelectionEscape),
            onSelectAll: () => _ = InvokeAsync(OnSelectionSelectAll));

        _currentDeviceId = DeviceStorageService.Get(PreferenceKeys.DEVICE_ID);
        await Task.WhenAll(LoadDevicesAsync(), LoadUserNamesAsync());
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

    /// <summary>Devices only carry user ids: names come from the user list, for display and search.</summary>
    private async Task LoadUserNamesAsync()
    {
        try
        {
            var users = await UserService.GetUsersAsync();
            _userNames = users.ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? "");
        }
        catch
        {
            // Best effort: the list falls back to a user count.
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
        if (!_selectionMode)
            return;

        _selectionMode = false;
        _selectedIds.Clear();
        StateHasChanged();
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
        StateHasChanged();
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

    private string GetDeviceName(DeviceDto device) =>
        string.IsNullOrWhiteSpace(device.DeviceName) ? L["UnknownDevice"] : device.DeviceName;

    /// <summary>Names of the device's users, or their count when names could not be loaded.</summary>
    private string GetUserNames(DeviceDto device)
    {
        if (device.Users.Count == 0)
            return "";

        var names = device.Users
            .Select(u => _userNames.GetValueOrDefault(u.Id, ""))
            .Where(name => name.Length > 0)
            .ToList();

        return names.Count > 0
            ? string.Join(", ", names)
            : string.Format(L["UserCount"], device.Users.Count);
    }

    /// <summary>Secondary line of the list: who uses the device and when it was last seen.</summary>
    private string GetRowSummary(DeviceDto device)
    {
        var users = GetUserNames(device);
        var lastSeen = device.LastSeen.LocalDateTime.ToString("g");
        return users.Length > 0 ? $"{users} - {lastSeen}" : lastSeen;
    }

    /// <summary>Searchable description: browser or client, system, last activity.</summary>
    private string GetDeviceSummary(DeviceDto device)
    {
        var parts = new List<string>(3)
        {
            device.WebDeviceDetails is not null
                ? GetBrowserLabel(device.WebDeviceDetails.Browser)
                : GetClientTypeLabel(device.ClientType)
        };

        var system = GetSystemLabel(device);
        if (system is not null)
            parts.Add(system);

        parts.Add(device.LastSeen.LocalDateTime.ToString("g"));

        return string.Join(" - ", parts);
    }

    private static string? GetSystemLabel(DeviceDto device)
    {
        var name = device.OperatingSystem == K7.Server.Domain.Enums.OperatingSystem.Unknown
            ? ""
            : device.OperatingSystem.ToString();
        var system = $"{name} {device.OperatingSystemVersion}".Trim();
        return system.Length == 0 ? null : system;
    }

    private static string? GetScreenLabel(DeviceDto device)
    {
        if (device.DisplayResolutionWidth > 0 && device.DisplayResolutionHeight > 0)
            return $"{device.DisplayResolutionWidth:0}x{device.DisplayResolutionHeight:0}";

        if (device.DisplayScreenWidth > 0 && device.DisplayScreenHeight > 0)
            return $"{device.DisplayScreenWidth:0}x{device.DisplayScreenHeight:0}";

        return null;
    }

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

    private string GetBrowserLabel(Browser browser) => browser switch
    {
        Browser.Chrome => "Chrome",
        Browser.Firefox => "Firefox",
        Browser.Edge => "Edge",
        Browser.Safari => "Safari",
        Browser.Opera => "Opera",
        _ => L["ClientTypeWeb"]
    };
}
