using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class TrackContextMenu
{
    [Parameter, EditorRequired]
    public required AudioQueueItem Track { get; set; }

    [Parameter]
    public int? UserRating { get; set; }

    [Inject] private IServerInfoService ServerInfo { get; set; } = default!;

    private bool _canCreatePlaylist;
    private bool _musicIntelligenceAvailable;

    protected override async Task OnInitializedAsync()
    {
        _canCreatePlaylist = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);

        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            _musicIntelligenceAvailable = status.IsAvailable;
        }
        catch
        {
            _musicIntelligenceAvailable = false;
        }
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
            { x => x.MediaId, Track.MediaId },
            { x => x.SourceMediaType, MediaType.MusicTrack }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<Dialogs.AddToPlaylistDialog>(L["AddToPlaylistTitle"], parameters, options);
    }

    private async Task RadioSonic()
    {
        await PlayServerRadioAsync(
            MusicRadioType.Sonic,
            string.Format(L["RadioSonicSnackbar"], Track.Title),
            seedTrackId: Track.MediaId);
    }

    private async Task RadioArtist()
    {
        if (Track.ArtistId is null)
            return;

        await PlayServerRadioAsync(
            MusicRadioType.Artist,
            string.Format(L["RadioArtistSnackbar"], Track.Artist),
            seedArtistId: Track.ArtistId);
    }

    private async Task RadioGenre()
    {
        if (string.IsNullOrEmpty(Track.Genre))
            return;

        var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            Genres = [Track.Genre],
            PageNumber = 1,
            PageSize = 200
        });

        var tracks = result?.Items?.OfType<LiteMusicTrackDto>()
            .Where(t => t.IndexedFileId.HasValue)
            .Select(ToQueueItem)
            .ToList();

        if (tracks is { Count: > 0 })
        {
            if (!Audio.Shuffle)
                Audio.ToggleShuffle();

            await Audio.PlayRadioAsync(tracks, string.Format(L["RadioGenreSnackbar"], Track.Genre));
        }
    }

    private async Task PlayServerRadioAsync(
        MusicRadioType radioType,
        string radioTitle,
        Guid? seedTrackId = null,
        Guid? seedArtistId = null)
    {
        var results = await ServerInfo.GetMusicRadioAsync(
            radioType.ToString(),
            seedTrackId: seedTrackId,
            seedArtistId: seedArtistId);

        var tracks = results?
            .OfType<MusicTrackDto>()
            .Select(t => MusicTrackQueueMapper.ToQueueItem(t, ApiClient, S["Untitled"]))
            .Where(t => t is not null)
            .Cast<AudioQueueItem>()
            .ToList();

        if (tracks is not { Count: > 0 })
            return;

        await Audio.PlayRadioAsync(tracks, radioTitle);
        Snackbar.Add(radioTitle, K7Severity.Info);
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

    private async Task DownloadOffline()
    {
        await DownloadManager.EnqueueAsync(new DownloadRequest
        {
            IndexedFileId = Track.IndexedFileId,
            MediaId = Track.MediaId,
            Title = Track.Title,
            Artist = Track.Artist,
            AlbumTitle = Track.AlbumTitle,
            CoverUrl = Track.CoverUrl,
            MediaType = MediaType.MusicTrack,
            IsCacheItem = false
        });
        Snackbar.Add(string.Format(L["DownloadQueued"], Track.Title), K7Severity.Info);
    }

    private async Task PlaySimilar()
    {
        try
        {
            var trackIds = await MusicIntelligence.GetSimilarTracksAsync(Track.MediaId);
            if (trackIds.Count == 0)
            {
                Snackbar.Add(L["NoSimilarTracks"], K7Severity.Warning);
                return;
            }

            var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                MediaTypes = [MediaType.MusicTrack],
                Ids = trackIds.ToArray(),
                PageNumber = 1,
                PageSize = trackIds.Count
            });

            var tracks = result?.Items?.OfType<LiteMusicTrackDto>()
                .Where(t => t.IndexedFileId.HasValue)
                .Select(t => ToQueueItem(t))
                .ToList();

            if (tracks is { Count: > 0 })
            {
                await Audio.PlayTracksAsync(tracks, 0);
                Snackbar.Add(string.Format(L["PlayingSimilar"], Track.Title), K7Severity.Info);
            }
        }
        catch
        {
            Snackbar.Add(L["SimilarTracksError"], K7Severity.Error);
        }
    }
}
