using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryBrowseRowActions : IDisposable
{
    [Parameter, EditorRequired] public MediaCardViewModel Model { get; set; } = default!;
    [Parameter] public string? Href { get; set; }
    [Parameter] public EventCallback OnRemove { get; set; }
    [Parameter] public string? RemoveAriaLabel { get; set; }
    [Parameter] public bool OverlayEnabled { get; set; } = true;
    [Parameter] public bool ExcludeMenuEnabled { get; set; }
    [Parameter] public bool WatchStateMenuEnabled { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public EventCallback OnExcludeForSelf { get; set; }
    [Parameter] public EventCallback OnExcludeForOthers { get; set; }
    [Parameter] public EventCallback OnWatchStateChanged { get; set; }

    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IMediaCardContextMenuService ContextMenuService { get; set; } = default!;

    private readonly Guid _menuOwnerId = Guid.NewGuid();
    private ElementReference _triggerRef;
    private bool _menuOpen;
    private bool _showRating;
    private bool _showReview;
    private bool _showPlaylist;
    private bool _showCollection;
    private bool _watchStateMenuVisible;
    private string? _menuCapabilitiesKey;

    private bool HasMenu =>
        (OverlayEnabled && !string.IsNullOrEmpty(Href))
        || ExcludeMenuEnabled
        || _watchStateMenuVisible
        || _showRating
        || _showReview
        || _showPlaylist
        || _showCollection;

    protected override void OnInitialized() =>
        ContextMenuService.Changed += OnContextMenuServiceChanged;

    protected override async Task OnParametersSetAsync()
    {
        if (Model is null)
        {
            _watchStateMenuVisible = false;
            _showRating = false;
            _showReview = false;
            _showPlaylist = false;
            _showCollection = false;
            _menuCapabilitiesKey = null;
            return;
        }

        var hasValidMediaId = Guid.TryParse(Model.Id, out _);
        // Key by kind/type/flags only - not media Id - so remounts reuse resolved flags.
        var capabilitiesKey = $"{Model.Kind}|{Model.MediaType}|{WatchStateMenuEnabled}|{hasValidMediaId}";
        if (_menuCapabilitiesKey == capabilitiesKey)
            return;

        _menuCapabilitiesKey = capabilitiesKey;

        var shared = await MediaCardMenuCapabilities.GetAsync(FeatureAccess);
        var mediaType = MediaCardMenuActions.InferMediaType(Model);

        _watchStateMenuVisible = hasValidMediaId
            && WatchStateMenuEnabled
            && WatchStateActions.SupportsWatchState(Model.Kind)
            && shared.CanSetWatchState;

        _showRating = hasValidMediaId && shared.CanRate;
        _showReview = hasValidMediaId && shared.CanRate && MediaCardMenuActions.SupportsReview(mediaType);
        _showPlaylist = hasValidMediaId && shared.CanCreateLibrary && MediaCardMenuActions.SupportsPlaylist(mediaType);
        _showCollection = hasValidMediaId && shared.CanCreateLibrary && MediaCardMenuActions.SupportsCollection(mediaType);
    }

    private void OnContextMenuServiceChanged()
    {
        var open = ContextMenuService.Current?.OwnerId == _menuOwnerId;
        if (open == _menuOpen)
            return;

        _menuOpen = open;
        InvokeAsync(StateHasChanged);
    }

    private Task OpenSharedMenuAsync()
    {
        if (!HasMenu || Model is null)
            return Task.CompletedTask;

        ContextMenuService.Open(new MediaCardContextMenuRequest
        {
            OwnerId = _menuOwnerId,
            Model = Model,
            Anchor = _triggerRef,
            AnchorKind = MediaCardContextMenuAnchorKind.Activator,
            Href = Href,
            Title = Model.Title,
            ShowPlay = OverlayEnabled && !string.IsNullOrEmpty(Href),
            ShowRating = _showRating,
            ShowReview = _showReview,
            ShowPlaylist = _showPlaylist,
            ShowCollection = _showCollection,
            ShowWatchState = _watchStateMenuVisible,
            ExcludeMenuEnabled = ExcludeMenuEnabled,
            IsAdmin = IsAdmin,
            OnExcludeForSelf = OnExcludeForSelf,
            OnExcludeForOthers = OnExcludeForOthers,
            OnWatchStateChanged = OnWatchStateChanged
        });

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        ContextMenuService.Changed -= OnContextMenuServiceChanged;
        if (_menuOpen)
            ContextMenuService.Close();
    }
}
