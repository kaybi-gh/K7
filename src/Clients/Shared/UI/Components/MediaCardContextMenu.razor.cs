using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaCardContextMenu
{
    [Parameter, EditorRequired]
    public MediaCardViewModel Model { get; set; } = default!;

    [Parameter]
    public string? Href { get; set; }

    [Parameter]
    public bool ShowPlay { get; set; }

    [Parameter]
    public bool ShowRating { get; set; }

    [Parameter]
    public bool ShowPlaylist { get; set; }

    [Parameter]
    public bool ShowCollection { get; set; }

    [Parameter]
    public bool ShowWatchState { get; set; }

    [Parameter]
    public bool ExcludeMenuEnabled { get; set; }

    [Parameter]
    public bool IsAdmin { get; set; }

    [Parameter]
    public int? BulkEpisodeCount { get; set; }

    [Parameter]
    public EventCallback OnExcludeForSelf { get; set; }

    [Parameter]
    public EventCallback OnExcludeForOthers { get; set; }

    [Parameter]
    public EventCallback OnWatchStateChanged { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> SharedStrings { get; set; } = default!;

    private Guid _mediaId;
    private bool _hasValidMediaId;

    protected override void OnParametersSet()
    {
        _hasValidMediaId = Model?.Id is not null && Guid.TryParse(Model.Id, out _mediaId);
    }

    private void OnPlay()
    {
        if (!string.IsNullOrEmpty(Href))
            NavigationManager.NavigateTo(Href);
    }

    private async Task ToggleWatchStateAsync()
    {
        var watched = !Model.Watched;
        var scope = WatchStateActions.GetScope(Model.Kind);
        var success = await WatchStateActions.ApplyAsync(
            MediaService,
            CacheStore,
            DialogService,
            Snackbar,
            SharedStrings,
            _mediaId,
            watched,
            scope,
            BulkEpisodeCount);

        if (!success)
            return;

        WatchStateActions.ApplyLocalCardState(Model, watched);
        if (OnWatchStateChanged.HasDelegate)
            await OnWatchStateChanged.InvokeAsync();

        await InvokeAsync(StateHasChanged);
    }

    private async Task AddToPlaylistAsync()
    {
        var parameters = new K7DialogParameters<AddToPlaylistDialog>
        {
            { x => x.MediaId, _mediaId },
            { x => x.SourceMediaType, MediaCardMenuActions.InferMediaType(Model) }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AddToPlaylistDialog>(L["AddToPlaylistTitle"], parameters, options);
    }

    private async Task AddToCollectionAsync()
    {
        var mediaType = MediaCardMenuActions.InferMediaType(Model);
        var parameters = new K7DialogParameters<AddToCollectionDialog>
        {
            { x => x.MediaId, _mediaId },
            { x => x.MediaType, mediaType }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AddToCollectionDialog>(L["AddToCollectionTitle"], parameters, options);
    }
}
