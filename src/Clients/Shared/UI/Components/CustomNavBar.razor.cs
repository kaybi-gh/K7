using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class CustomNavBar : IAsyncDisposable
{
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

    protected override async Task OnInitializedAsync()
    {
        Store.Changed += OnStoreChanged;
        NavigationManager.LocationChanged += OnLocationChanged;
        _isNativeClient = DeviceService.GetClientType() != ClientType.Web;
        _device = DeviceService.CachedDeviceType ?? await DeviceService.GetDeviceTypeAsync();
        await Store.EnsureLoadedAsync();
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
        _overflowMeasured = false;
        _visibleCount = int.MaxValue;
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

    private string GetHref(CustomNavItemDto item) =>
        CustomNavHrefHelper.GetHref(item, Store.Groups, Store.GeneralPreferences, Store.Collections, Store.Playlists);

    private string GetTitle(CustomNavItemDto item) =>
        CustomNavHrefHelper.GetTitle(item, Store.Groups, key => NavL[key], Store.Collections, Store.Playlists);

    private void Navigate(string href) => LibraryGroupBrowseUrlSync.Navigate(NavigationManager, href);

    private void OnStoreChanged() => InvokeAsync(StateHasChanged);

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
}
