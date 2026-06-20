using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpacePlaylistsPage
{
    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;
    private bool _canCreate;
    private MediaType? _mediaTypeFilter;
    private List<ButtonGroupOption<MediaType?>> _mediaTypeOptions = [];

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _mediaTypeOptions =
        [
            new(null, Label: L["All"]),
            new(MediaType.MusicTrack, Label: L["Music"]),
            new(MediaType.Movie, Label: L["FilterMovies"]),
            new(MediaType.SerieEpisode, Label: L["TVShows"])
        ];

        _canCreate = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        await LoadPlaylistsAsync();
    }

    private async Task LoadPlaylistsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetPlaylistsAsync(mediaType: _mediaTypeFilter);
        _playlists = result?.Items?.ToList() ?? [];
        _loading = false;
    }

    private async Task SetMediaTypeFilter(MediaType? mediaType)
    {
        _mediaTypeFilter = mediaType;
        await LoadPlaylistsAsync();
    }

    private async Task OpenCreatePlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreatePlaylistDialog>("Nouvelle playlist", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadPlaylistsAsync();
    }

    private async Task OpenCreateSmartPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SmartPlaylistDialog>("Nouvelle smart playlist", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid id })
        {
            try { await K7ServerService.EvaluateSmartPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/smart-playlists/{id}");
        }
    }

    private void GoToPlaylist(LitePlaylistDto playlist)
    {
        var url = playlist.IsSmartPlaylist
            ? $"/smart-playlists/{playlist.Id}"
            : $"/playlists/{playlist.Id}";
        NavigationManager.NavigateTo(url);
    }

    private void OnPlaylistKeyDown(KeyboardEventArgs e, LitePlaylistDto playlist)
    {
        if (e.Code is "Enter" or "Space")
            GoToPlaylist(playlist);
    }

    private string? GetCoverUrl(LitePlaylistDto playlist)
    {
        var uri = playlist.CoverPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString;
        return ApiClient.GetAbsoluteUri(uri)?.AbsoluteUri;
    }

    private string GetItemLabel(MediaType mediaType) => mediaType switch
    {
        MediaType.MusicTrack => L["Tracks"],
        MediaType.Movie => L["Movies"],
        MediaType.SerieEpisode => L["Episodes"],
        _ => L["Items"]
    };
}
