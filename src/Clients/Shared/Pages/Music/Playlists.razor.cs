using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Playlists;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.Pages.Music;

public partial class Playlists
{
    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadPlaylistsAsync();
    }

    private async Task LoadPlaylistsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetPlaylistsAsync();
        _playlists = result?.Items?.ToList() ?? [];
        _loading = false;
    }

    private async Task OpenCreateDialog()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<K7.Clients.Shared.Components.Dialogs.CreatePlaylistDialog>("Nouvelle playlist", options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadPlaylistsAsync();
        }
    }

    private void GoToPlaylist(Guid id)
    {
        NavigationManager.NavigateTo($"/playlists/{id}");
    }

    private string? GetCoverUrl(LitePlaylistDto playlist)
    {
        return K7ServerService.GetAbsoluteUri(
            playlist.CoverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
    }
}
