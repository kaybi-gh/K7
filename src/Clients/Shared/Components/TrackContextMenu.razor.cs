using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.Components;

public partial class TrackContextMenu
{
    [Parameter, EditorRequired]
    public required AudioQueueItem Track { get; set; }

    private void PlayNext()
    {
        Audio.AddToQueueNext(Track);
        Snackbar.Add($"« {Track.Title} » joué ensuite", Severity.Info);
    }

    private void AddToQueue()
    {
        Audio.AddToQueue(Track);
        Snackbar.Add($"« {Track.Title} » ajouté à la queue", Severity.Info);
    }

    private async Task RadioArtist()
    {
        if (Track.ArtistPersonId is null) return;

        var result = await K7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            PersonIds = [Track.ArtistPersonId.Value],
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
            Snackbar.Add($"Radio {Track.Artist}", Severity.Info);
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
            Snackbar.Add($"Radio {Track.Genre}", Severity.Info);
        }
    }

    private AudioQueueItem ToQueueItem(LiteMusicTrackDto t) => new()
    {
        IndexedFileId = t.IndexedFileId!.Value,
        MediaId = t.Id,
        Title = t.Title ?? "Sans titre",
        Artist = t.ArtistName,
        AlbumTitle = t.AlbumTitle,
        ArtistPersonId = t.ArtistPersonId,
        Genre = t.Genre,
        CoverUrl = K7ServerService.GetAbsoluteUri(
            t.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
        Duration = t.Duration
    };
}
