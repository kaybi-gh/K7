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
    private bool? _isTv;
    private bool _hubPageActive;
    private Guid? _initializedGroupId;
    private LibraryGroupDto? _group;
    private ExploreFocusNavigationContext? _focusNavigation;
    private bool _feedRestoreLoadFailed;
    private IJSObjectReference? _feedRestoreModule;

    private FeedHubKey PageKey => FeedHubKey.ForExploreGroup(GroupId);

    private string _pageClass => _isTv == true
        ? "tv-feed-page"
        : "explore-group-page page-scrollable";

    private string? _initialFocus => _isTv == true
        ? "[data-carousel-item] a, [data-carousel-item] button"
        : null;

    protected override void OnInitialized()
    {
        if (DeviceService.CachedDeviceType is { } cached)
            _isTv = cached == DeviceType.TV;

        FeedHub.Changed += OnFeedHubChanged;
        _hubPageActive = IsHubPageActive();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_initializedGroupId == GroupId)
            return;

        _initializedGroupId = GroupId;
        _isTv ??= await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
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

    public void Dispose()
    {
        FeedHub.Changed -= OnFeedHubChanged;
        if (_feedRestoreModule is not null)
        {
            _ = _feedRestoreModule.DisposeAsync().AsTask();
            _feedRestoreModule = null;
        }
    }

    private bool IsHubPageActive() =>
        FeedHub.IsHubRouteActive && FeedHub.ActiveKey == PageKey;

    private void OnFeedHubChanged()
    {
        var active = IsHubPageActive();
        var becameActive = active && !_hubPageActive;
        _hubPageActive = active;

        if (!becameActive)
            return;

        InvokeAsync(OnHubPageBecameActiveAsync).FireAndForget();
    }

    private async Task OnHubPageBecameActiveAsync()
    {
        await Task.Yield();
        await Task.Delay(50);
        await RestoreLastFocusedCardAsync();
    }

    private async Task RestoreLastFocusedCardAsync()
    {
        var mediaId = HubFocus.GetMediaId(PageKey);
        if (string.IsNullOrEmpty(mediaId) || _focusNavigation is null)
            return;

        var cardId = _focusNavigation.GetCardElementId(mediaId);

        if (_isTv == true)
        {
            try
            {
                if (_feedRestoreModule is null && !_feedRestoreLoadFailed)
                {
                    _feedRestoreModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                        "import", "./_content/K7.Clients.Shared.UI/js/home-restore.js");
                }

                if (_feedRestoreModule is not null)
                    await _feedRestoreModule.InvokeAsync<bool>("scrollToCardById", cardId);
            }
            catch (JSException)
            {
                _feedRestoreLoadFailed = true;
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        try
        {
            await JSRuntime.InvokeAsync<bool>("K7.focusById", cardId, true);
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
