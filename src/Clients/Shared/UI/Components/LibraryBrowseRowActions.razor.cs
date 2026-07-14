using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryBrowseRowActions
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

    private bool _menuOpen;
    private bool _showRating;
    private bool _showReview;
    private bool _showPlaylist;
    private bool _showCollection;
    private bool _watchStateMenuVisible;

    private bool HasMenu =>
        (OverlayEnabled && !string.IsNullOrEmpty(Href))
        || ExcludeMenuEnabled
        || _watchStateMenuVisible
        || _showRating
        || _showReview
        || _showPlaylist
        || _showCollection;

    protected override async Task OnParametersSetAsync()
    {
        if (Model is null)
        {
            _watchStateMenuVisible = false;
            _showRating = false;
            _showReview = false;
            _showPlaylist = false;
            _showCollection = false;
            return;
        }

        var hasValidMediaId = Guid.TryParse(Model.Id, out _);

        _watchStateMenuVisible = hasValidMediaId
            && WatchStateMenuEnabled
            && WatchStateActions.SupportsWatchState(Model.Kind)
            && await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        var canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
        var canCreateLibrary = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        var mediaType = MediaCardMenuActions.InferMediaType(Model);

        _showRating = hasValidMediaId && canRate;
        _showReview = hasValidMediaId && canRate && MediaCardMenuActions.SupportsReview(mediaType);
        _showPlaylist = hasValidMediaId && canCreateLibrary && MediaCardMenuActions.SupportsPlaylist(mediaType);
        _showCollection = hasValidMediaId && canCreateLibrary && MediaCardMenuActions.SupportsCollection(mediaType);
    }

    private void OnMenuOpenChanged(bool open) => _menuOpen = open;
}
