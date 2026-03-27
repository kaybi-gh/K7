using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicAlbumDetail
{
    [Parameter]
    public required string Id { get; set; }

    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    private MusicAlbumDto? _album;
    private string? _coverUrl;
    private string? _coverDominantColor;
    private List<ArtistInfo> _artists = [];
    private List<TrackViewModel> _tracks = [];
    private SortedDictionary<int, List<TrackViewModel>> _tracksByDisc = [];
    private int _trackCount;
    private double _totalDuration;
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;

        var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
        if (media is MusicAlbumDto album)
        {
            _album = album;

            var coverPicture = album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);
            _coverUrl = apiClient.GetAbsoluteUri(
                coverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _coverDominantColor = coverPicture?.DominantColor;

            _artists = album.PersonRoles?
                .OfType<LiteMusicArtistRoleDto>()
                .Where(r => r.Person is not null)
                .Select(r => new ArtistInfo(r.Person!.Id, r.Person.Name ?? S["UnknownArtist"]))
                .DistinctBy(a => a.PersonId)
                .ToList() ?? [];

            var artistName = _artists.Count > 0
                ? string.Join(", ", _artists.Select(a => a.Name))
                : null;

            _tracks = (album.Tracks ?? [])
                .OrderBy(t => t.TrackNumber)
                .Select(t => new TrackViewModel
                {
                    Id = t.Id,
                    IndexedFileId = t.IndexedFileId,
                    Title = t.Title ?? S["Untitled"],
                    TrackNumber = t.TrackNumber,
                    ArtistName = artistName,
                    ArtistPersonId = _artists.FirstOrDefault()?.PersonId,
                    Genre = album.Genres?.FirstOrDefault(),
                    CoverUrl = _coverUrl,
                    CoverDominantColor = _coverDominantColor,
                    Duration = t.Duration ?? 0,
                    DiscNumber = 1,
                    Bpm = t.Bpm,
                    MusicalKey = t.MusicalKey,
                    Energy = t.Energy,
                    IsPlaying = Audio.CurrentTrack?.MediaId == t.Id
                })
                .ToList();

            _tracksByDisc = new SortedDictionary<int, List<TrackViewModel>>(
                _tracks.GroupBy(t => t.DiscNumber).ToDictionary(g => g.Key, g => g.ToList()));

            _trackCount = _tracks.Count;
            _totalDuration = _tracks.Sum(t => t.Duration);
        }

        _loading = false;
    }

    private async Task PlayAll()
    {
        var queueItems = BuildQueueItems();
        if (queueItems.Count > 0)
            await Audio.PlayTracksAsync(queueItems, 0);
    }

    private async Task ShuffleAll()
    {
        var queueItems = BuildQueueItems();
        if (queueItems.Count > 0)
        {
            if (!Audio.Shuffle) Audio.ToggleShuffle();
            await Audio.PlayTracksAsync(queueItems, 0);
        }
    }

    private async Task OnTrackClick(TableRowClickEventArgs<TrackViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;

        var queueItems = BuildQueueItems();
        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
    }

    private List<AudioQueueItem> BuildQueueItems()
    {
        return _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();
    }

    private AudioQueueItem BuildQueueItem(TrackViewModel t)
    {
        var artistName = _artists.Count > 0
            ? string.Join(", ", _artists.Select(a => a.Name))
            : null;

        return new AudioQueueItem
        {
            IndexedFileId = t.IndexedFileId!.Value,
            MediaId = t.Id,
            Title = t.Title,
            Artist = artistName,
            ArtistPersonId = t.ArtistPersonId,
            AlbumTitle = _album?.Title,
            Genre = t.Genre,
            CoverUrl = t.CoverUrl,
            CoverDominantColor = t.CoverDominantColor,
            Duration = t.Duration,
            UserRating = t.UserRating,
            Bpm = t.Bpm,
            MusicalKey = t.MusicalKey,
            Energy = t.Energy
        };
    }

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    private static string FormatTotalDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours} h {ts.Minutes:00} min";
        return $"{ts.Minutes} min";
    }

    internal sealed record ArtistInfo(Guid PersonId, string Name);

    internal sealed record TrackViewModel
    {
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public int? TrackNumber { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistPersonId { get; init; }
        public string? Genre { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public double Duration { get; init; }
        public int DiscNumber { get; init; }
        public int? UserRating { get; init; }
        public double? Bpm { get; init; }
        public string? MusicalKey { get; init; }
        public double? Energy { get; init; }
        public bool IsPlaying { get; init; }
    }
}
