using System.Text.Json;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class CustomNavBar : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = K7JsonSerializerOptions.CreateDefault();

    private readonly string _jsId = Guid.NewGuid().ToString("N");
    private DeviceType _device = DeviceType.Desktop;
    private bool _isNativeClient;
    private int _visibleCount = int.MaxValue;
    private ElementReference _barRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<CustomNavBar>? _dotnetRef;
    private bool _attached;
    private bool _overflowMeasured;
    private string? _measuredKey;
    private bool _disposed;

    [Inject] private ICustomNavStore Store { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IStringLocalizer<CustomNavStrings> NavL { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IDeviceStorageService Storage { get; set; } = default!;

    private string CurrentPath => CustomNavPath.From(NavigationManager);

    private IReadOnlyList<CustomNavItemDto> VisibleItems =>
        CustomNavRoutes.FilterForClient(Store.Layout.Items, _isNativeClient);

    internal bool Visible =>
        CustomNavPath.ShouldShowBar(Store.Layout, CurrentPath, _device)
        && VisibleItems.Count > 0;

    private int VisibleItemCount =>
        VisibleItems.Count == 0 ? 0 : Math.Min(_visibleCount, VisibleItems.Count);

    private bool HasOverflow => VisibleItemCount < VisibleItems.Count;

    private bool OverflowHasActive
    {
        get
        {
            var path = CurrentPath;
            foreach (var item in VisibleItems.Skip(VisibleItemCount))
            {
                if (CustomNavVisibility.IsActive(GetHref(item), path))
                    return true;
            }

            return false;
        }
    }

    private string BarLabel =>
        string.IsNullOrWhiteSpace(Store.Layout.FeedRowTitle)
            ? NavL["FeedRowTitle"]
            : Store.Layout.FeedRowTitle;

    private string MeasureKey =>
        $"{Store.Layout.BarShowIcons}:{string.Join('|', VisibleItems.Select(i => $"{i.Id}:{GetTitle(i)}:{CustomNavHrefHelper.GetIcon(i, Store.Groups, Store.Collections, Store.Playlists)}"))}";

    protected override void OnInitialized()
    {
        Store.Changed += OnStoreChanged;
        NavigationManager.LocationChanged += OnLocationChanged;
        _isNativeClient = DeviceService.GetClientType() != ClientType.Web;
        if (DeviceService.CachedDeviceType is { } cached)
            _device = cached;

        RestoreOverflow();
    }

    protected override async Task OnInitializedAsync()
    {
        if (DeviceService.CachedDeviceType is null)
            _device = await DeviceService.GetDeviceTypeAsync();

        await Store.EnsureLoadedAsync();
        RestoreOverflow();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        if (!Visible)
        {
            await DetachObserverAsync();
            return;
        }

        await EnsureModuleAsync();
        if (_disposed || _module is null)
            return;

        var key = MeasureKey;
        if (!_attached)
        {
            _dotnetRef ??= DotNetObjectReference.Create(this);
            try
            {
                await _module.InvokeVoidAsync("attach", _jsId, _barRef, _dotnetRef);
                _attached = true;
                _measuredKey = key;
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }

            return;
        }

        if (_measuredKey == key)
            return;

        _measuredKey = key;
        try
        {
            await _module.InvokeVoidAsync("measure", _jsId);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    [JSInvokable]
    public void SetVisibleCount(int count)
    {
        if (_disposed)
            return;

        var next = Math.Clamp(count, 0, Math.Max(VisibleItems.Count, 0));
        if (_overflowMeasured && next == _visibleCount)
            return;

        _visibleCount = next;
        _overflowMeasured = true;
        PersistOverflow();
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task EnsureModuleAsync()
    {
        if (_module is not null)
            return;

        try
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/customNavBar.js");
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private async Task DetachObserverAsync()
    {
        _measuredKey = null;

        if (!_attached || _module is null)
        {
            _attached = false;
            return;
        }

        _attached = false;
        try
        {
            await _module.InvokeVoidAsync("dispose", _jsId);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private void RestoreOverflow()
    {
        if (_overflowMeasured)
            return;

        var userId = Storage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID);
        var json = Storage.Get(PreferenceKeys.CUSTOM_NAV_BAR_MEASURE);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrWhiteSpace(json))
            return;

        CustomNavBarOverflowEntry? entry;
        try
        {
            entry = JsonSerializer.Deserialize<CustomNavBarOverflowEntry>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (entry is null
            || !string.Equals(entry.UserId, userId, StringComparison.Ordinal)
            || entry.Key != MeasureKey
            || entry.Count <= 0
            || VisibleItems.Count == 0)
            return;

        _visibleCount = Math.Clamp(entry.Count, 1, VisibleItems.Count);
        _overflowMeasured = true;
    }

    private void PersistOverflow()
    {
        var userId = Storage.Get(PreferenceKeys.LAST_ACTIVE_USER_ID);
        if (string.IsNullOrEmpty(userId))
            return;

        var entry = new CustomNavBarOverflowEntry
        {
            UserId = userId,
            Key = MeasureKey,
            Count = _visibleCount
        };

        try
        {
            Storage.Set(PreferenceKeys.CUSTOM_NAV_BAR_MEASURE, JsonSerializer.Serialize(entry, JsonOptions));
        }
        catch (InvalidOperationException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private string GetHref(CustomNavItemDto item) =>
        CustomNavHrefHelper.GetHref(item, Store.Groups, Store.GeneralPreferences, Store.Collections, Store.Playlists);

    private string GetTitle(CustomNavItemDto item) =>
        CustomNavHrefHelper.GetTitle(item, Store.Groups, key => NavL[key], Store.Collections, Store.Playlists);

    private void Navigate(string href) => LibraryGroupBrowseUrlSync.Navigate(NavigationManager, href);

    private void OnStoreChanged() => InvokeAsync(() =>
    {
        if (!_overflowMeasured)
            RestoreOverflow();

        StateHasChanged();
    });

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) =>
        InvokeAsync(StateHasChanged);

    internal static string ToPhosphorClass(string icon) =>
        icon.StartsWith("ph ", StringComparison.Ordinal) ? icon : $"ph ph-{icon}";

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        Store.Changed -= OnStoreChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
        await DetachObserverAsync();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }

            _module = null;
        }

        _dotnetRef?.Dispose();
        _dotnetRef = null;
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;

    private sealed class CustomNavBarOverflowEntry
    {
        public string UserId { get; set; } = "";

        public string Key { get; set; } = "";

        public int Count { get; set; }
    }
}
