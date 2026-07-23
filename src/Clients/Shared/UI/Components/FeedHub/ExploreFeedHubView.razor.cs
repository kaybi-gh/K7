using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Explore;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.FeedHub;

public partial class ExploreFeedHubView : IDisposable
{
    [Parameter, EditorRequired] public Guid GroupId { get; set; }

    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IFeedHubHostService FeedHub { get; set; } = default!;
    [Inject] private IHubFocusNavigationState HubFocus { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool _loading = true;
    private bool _isTv;
    private bool _hubPageActive;
    private LibraryGroupDto? _group;
    private ExploreFocusNavigationContext? _focusNavigation;

    private FeedHubKey PageKey => FeedHubKey.ForExploreGroup(GroupId);

    private string _pageClass => _isTv
        ? "tv-feed-page"
        : "explore-group-page page-scrollable";

    private string? _initialFocus => _isTv
        ? "[data-carousel-item] a, [data-carousel-item] button"
        : null;

    protected override async Task OnParametersSetAsync()
    {
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        _loading = true;
        var snapshot = await ExploreGroupStore.EnsureGroupAsync(GroupId);
        _group = snapshot?.Group;
        _focusNavigation = new ExploreFocusNavigationContext
        {
            GroupId = GroupId,
            SaveMediaId = mediaId => HubFocus.Save(PageKey, mediaId)
        };
        _loading = false;
    }

    protected override void OnInitialized()
    {
        FeedHub.Changed += OnFeedHubChanged;
        _hubPageActive = IsHubPageActive();
    }

    public void Dispose() => FeedHub.Changed -= OnFeedHubChanged;

    private bool IsHubPageActive() =>
        FeedHub.IsHubRouteActive && FeedHub.ActiveKey == PageKey;

    private void OnFeedHubChanged()
    {
        var active = IsHubPageActive();
        var becameActive = active && !_hubPageActive;
        _hubPageActive = active;

        if (!becameActive)
            return;

        InvokeAsync(RestoreLastFocusedCardAsync).FireAndForget();
    }

    private async Task RestoreLastFocusedCardAsync()
    {
        var mediaId = HubFocus.GetMediaId(PageKey);
        if (string.IsNullOrEmpty(mediaId) || _focusNavigation is null)
            return;

        try
        {
            await JSRuntime.InvokeVoidAsync(
                "K7.focusById",
                _focusNavigation.GetCardElementId(mediaId),
                true);
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
