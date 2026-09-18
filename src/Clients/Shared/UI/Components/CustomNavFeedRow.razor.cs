using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class CustomNavFeedRow : IDisposable
{
    [Parameter] public bool IsGroupFeed { get; set; }

    [Inject] private ICustomNavStore Store { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IStringLocalizer<CustomNavStrings> NavL { get; set; } = default!;

    private bool _deviceReady;
    private DeviceType _device = DeviceType.Desktop;
    private bool _isNativeClient;

    private IReadOnlyList<CustomNavItemDto> VisibleItems =>
        CustomNavRoutes.FilterForClient(Store.Layout.Items, _isNativeClient);

    private bool Visible => _deviceReady
        && VisibleItems.Count > 0
        && (IsGroupFeed
            ? CustomNavVisibility.ShouldShowGroupFeedRow(Store.Layout, _device)
            : CustomNavVisibility.ShouldShowHomeRow(Store.Layout, _device));

    private string RowTitle => Store.Layout.FeedRowTitle?.Trim() ?? "";

    private string ContentKey => string.Join(',', VisibleItems.Select(i => i.Id));

    protected override void OnInitialized()
    {
        Store.Changed += OnStoreChanged;
        _isNativeClient = DeviceService.GetClientType() != ClientType.Web;
        if (DeviceService.CachedDeviceType is { } cached)
        {
            _device = cached;
            _deviceReady = true;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (!_deviceReady)
        {
            _device = await DeviceService.GetDeviceTypeAsync();
            _deviceReady = true;
        }

        await Store.EnsureLoadedAsync();
    }

    private string GetHref(CustomNavItemDto item) =>
        CustomNavHrefHelper.GetHref(item, Store.Groups, Store.GeneralPreferences, Store.Collections, Store.Playlists);

    private string GetTitle(CustomNavItemDto item) =>
        CustomNavHrefHelper.GetTitle(item, Store.Groups, key => NavL[key], Store.Collections, Store.Playlists);

    private string? GetImageUrl(CustomNavItemDto item)
    {
        var pictureId = CustomNavHrefHelper.GetCoverPictureId(item, Store.Groups, Store.Collections, Store.Playlists);
        return pictureId is { } id
            ? $"/api/metadata-pictures/{id}?size=Medium"
            : null;
    }

    private void Navigate(string href) => LibraryGroupBrowseUrlSync.Navigate(NavigationManager, href);

    private void OnStoreChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Store.Changed -= OnStoreChanged;
}
