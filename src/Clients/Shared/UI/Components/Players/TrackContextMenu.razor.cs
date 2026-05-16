using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class TrackContextMenu
{
    [Parameter, EditorRequired]
    public required AudioQueueItem Track { get; set; }

    private bool _canCreatePlaylist;

    protected override async Task OnInitializedAsync()
    {
        _canCreatePlaylist = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
    }

    private void PlayNext()
    {
        Audio.AddToQueueNext(Track);
        Snackbar.Add(string.Format(L["PlayNextSnackbar"], Track.Title), K7Severity.Info);
    }

    private void AddToQueue()
    {
        Audio.AddToQueue(Track);
        Snackbar.Add(string.Format(L["AddedToQueueSnackbar"], Track.Title), K7Severity.Info);
    }

    private async Task AddToPlaylist()
    {
        var parameters = new K7DialogParameters<Dialogs.AddToPlaylistDialog>
        {
            { x => x.MediaId, Track.MediaId }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<Dialogs.AddToPlaylistDialog>(L["AddToPlaylistTitle"], parameters, options);
    }

    private async Task RadioArtist()
    {
        if (Track.ArtistId is null) return;

        var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            ArtistIds = [Track.ArtistId.Value],
            PageNumber = 1,
            PageSize = 200
        });

        var tracks = result?.Items?.OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => ToQueueItem(t))
            .ToList();

        if (tracks is { Count: > 0 })
        {
            if (!Audio.Shuffle) Audio.ToggleShuffle();
            await Audio.PlayTracksAsync(tracks, 0);
            Snackbar.Add(string.Format(L["RadioArtistSnackbar"], Track.Artist), K7Severity.Info);
        }
    }

    private async Task RadioGenre()
    {
        if (string.IsNullOrEmpty(Track.Genre)) return;

        var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            Genres = [Track.Genre],
            PageNumber = 1,
            PageSize = 200
        });

        var tracks = result?.Items?.OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => ToQueueItem(t))
            .ToList();

        if (tracks is { Count: > 0 })
        {
            if (!Audio.Shuffle) Audio.ToggleShuffle();
            await Audio.PlayTracksAsync(tracks, 0);
            Snackbar.Add(string.Format(L["RadioGenreSnackbar"], Track.Genre), K7Severity.Info);
        }
    }

    private AudioQueueItem ToQueueItem(LiteMusicTrackDto t) => new()
    {
        IndexedFileId = t.IndexedFileId!.Value,
        MediaId = t.Id,
        Title = t.Title ?? S["Untitled"],
        Artist = t.ArtistName,
        AlbumTitle = t.AlbumTitle,
        ArtistId = t.ArtistId,
        Genre = t.Genre,
        CoverUrl = ApiClient.GetAbsoluteUri(
            (t.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? t.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
        Duration = t.Duration
    };
}
