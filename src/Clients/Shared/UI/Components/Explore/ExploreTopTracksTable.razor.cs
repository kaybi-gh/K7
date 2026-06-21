using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Explore;

public partial class ExploreTopTracksTable
{
    [Parameter] public Guid[] LibraryIds { get; set; } = [];

    private List<TrackRow> _tracks = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await MediaService.GetTopMusicTracksAsync(
                LibraryIds.Length > 0 ? LibraryIds : null,
                count: 20);

            _tracks = result
                .Select((item, index) => ToRow(item, index + 1))
                .Where(row => row.IndexedFileId.HasValue)
                .ToList();
        }
        catch
        {
            _tracks = [];
        }

        _loading = false;
    }

    private async Task OnTrackClick(TableRowClickEventArgs<TrackRow> args)
    {
        var track = args.Item;
        if (track is null)
            return;

        var queueItems = _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();

        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
    }

    private TrackRow ToRow(PlayedMusicTrackDto item, int rank)
    {
        var track = item.Track;
        return new TrackRow
        {
            Rank = rank,
            Id = track.Id,
            IndexedFileId = track.IndexedFileId,
            Title = track.Title ?? S["Untitled"],
            ArtistName = track.ArtistName,
            ArtistId = track.ArtistId,
            AlbumId = track.AlbumId,
            AlbumTitle = track.AlbumTitle,
            Duration = track.Duration ?? 0,
            PlayCount = item.PlayCount,
            CoverUrl = ApiClient.GetAbsoluteUri(
                (track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                    ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            CoverDominantColor = (track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?.DominantColor,
            Genre = track.Genre,
            UserRating = track.UserRating,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            Energy = track.Energy,
            IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
        };
    }

    private static AudioQueueItem BuildQueueItem(TrackRow track) => new()
    {
        IndexedFileId = track.IndexedFileId!.Value,
        MediaId = track.Id,
        Title = track.Title,
        Artist = track.ArtistName,
        ArtistId = track.ArtistId,
        AlbumTitle = track.AlbumTitle,
        Genre = track.Genre,
        CoverUrl = track.CoverUrl,
        CoverDominantColor = track.CoverDominantColor,
        Duration = track.Duration,
        UserRating = track.UserRating,
        Bpm = track.Bpm,
        MusicalKey = track.MusicalKey,
        Energy = track.Energy
    };

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0)
            return "";

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    internal sealed record TrackRow
    {
        public int Rank { get; init; }
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistId { get; init; }
        public Guid AlbumId { get; init; }
        public string? AlbumTitle { get; init; }
        public double Duration { get; init; }
        public int PlayCount { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public string? Genre { get; init; }
        public int? UserRating { get; init; }
        public double? Bpm { get; init; }
        public string? MusicalKey { get; init; }
        public double? Energy { get; init; }
        public bool IsPlaying { get; init; }
    }
}
