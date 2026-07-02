using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class AddToPlaylistDialog
{
    [Inject] private IPlaylistService K7ServerService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public Guid MediaId { get; set; }

    [Parameter]
    public MediaType? SourceMediaType { get; set; }

    private List<LitePlaylistDto> _playlists = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadPlaylists();
    }

    private async Task LoadPlaylists()
    {
        _loading = true;
        var result = await K7ServerService.GetPlaylistsAsync(1, 100);
        var playlists = result?.Items?.Where(playlist => !playlist.IsSmartPlaylist) ?? [];

        var targetMediaType = MediaPlaylistAddHelper.GetTargetPlaylistMediaType(SourceMediaType);
        if (targetMediaType.HasValue)
            playlists = playlists.Where(playlist => playlist.MediaType == targetMediaType.Value);

        _playlists = playlists.ToList();
        _loading = false;
    }

    private async Task SelectPlaylist(LitePlaylistDto playlist)
    {
        try
        {
            var addedCount = await MediaPlaylistAddHelper.AddMediaToPlaylistAsync(
                K7ServerService,
                MediaService,
                playlist.Id,
                MediaId,
                SourceMediaType);

            if (addedCount == 0)
            {
                Snackbar.Add(L["NoTracksToAdd"], K7Severity.Warning);
                return;
            }

            var message = addedCount > 1
                ? string.Format(L["AddedTracksToPlaylist"], addedCount, playlist.Title)
                : string.Format(L["AddedToPlaylist"], playlist.Title);
            Snackbar.Add(message, K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(playlist.Id));
        }
        catch
        {
            Snackbar.Add(L["AddError"], K7Severity.Error);
        }
    }

    private async Task CreateNewPlaylist()
    {
        var defaultMediaType = MediaPlaylistAddHelper.GetTargetPlaylistMediaType(SourceMediaType)
            ?? MediaType.MusicTrack;
        var parameters = new K7DialogParameters<CreatePlaylistDialog>
        {
            { x => x.DefaultMediaType, defaultMediaType }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreatePlaylistDialog>(L["NewPlaylistTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid newId })
        {
            try
            {
                var addedCount = await MediaPlaylistAddHelper.AddMediaToPlaylistAsync(
                    K7ServerService,
                    MediaService,
                    newId,
                    MediaId,
                    SourceMediaType);

                if (addedCount == 0)
                {
                    Snackbar.Add(L["CreatedButNoTracksAdded"], K7Severity.Warning);
                    Dialog.Close(K7DialogResult.Ok(newId));
                    return;
                }

                var message = addedCount > 1
                    ? string.Format(L["AddedTracksToNewPlaylist"], addedCount)
                    : L["AddedToNewPlaylist"];
                Snackbar.Add(message, K7Severity.Success);
                Dialog.Close(K7DialogResult.Ok(newId));
            }
            catch
            {
                Snackbar.Add(L["CreatedButAddFailed"], K7Severity.Warning);
                Dialog.Close(K7DialogResult.Ok(newId));
            }
        }
        else
        {
            await LoadPlaylists();
        }
    }

    private void Cancel() => Dialog.Cancel();

    private string FormatItemCount(LitePlaylistDto playlist) => playlist.MediaType switch
    {
        MediaType.MusicTrack => string.Format(L["TrackCount"], playlist.ItemCount),
        MediaType.Movie => string.Format(L["MovieCount"], playlist.ItemCount),
        MediaType.SerieEpisode => string.Format(L["EpisodeCount"], playlist.ItemCount),
        _ => string.Format(L["ItemCount"], playlist.ItemCount)
    };
}
