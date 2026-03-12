using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.Pages.Music;

public partial class MusicTracks
{
    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    private List<TrackViewModel> _tracks = [];
    private bool _loading = true;
    private string? _searchText;

    protected override async Task OnInitializedAsync()
    {
        await LoadTracksAsync();
    }

    private async Task LoadTracksAsync()
    {
        _loading = true;

        var result = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 500
        });

        if (result?.Items is not null)
        {
            _tracks = result.Items
                .OfType<LiteMusicTrackDto>()
                .Where(t => string.IsNullOrEmpty(_searchText) ||
                            (t.Title?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (t.ArtistName?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (t.AlbumTitle?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(ToViewModel)
                .ToList();
        }

        _loading = false;
    }

    private async Task OnSearchChanged(string? text)
    {
        _searchText = text;
        await LoadTracksAsync();
    }

    private async Task OnTrackClick(TableRowClickEventArgs<TrackViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;

        var queueItems = _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();

        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
    }

    private static AudioQueueItem BuildQueueItem(TrackViewModel t) => new()
    {
        IndexedFileId = t.IndexedFileId!.Value,
        MediaId = t.Id,
        Title = t.Title,
        Artist = t.ArtistName,
        ArtistPersonId = t.ArtistPersonId,
        AlbumTitle = t.AlbumTitle,
        Genre = t.Genre,
        CoverUrl = t.CoverUrl,
        CoverDominantColor = t.CoverDominantColor,
        Duration = t.Duration,
        UserRating = t.UserRating,
        Bpm = t.Bpm,
        MusicalKey = t.MusicalKey,
        Energy = t.Energy
    };

    private TrackViewModel ToViewModel(LiteMusicTrackDto track) => new()
    {
        Id = track.Id,
        IndexedFileId = track.IndexedFileId,
        Title = track.Title ?? "Sans titre",
        ArtistName = track.ArtistName,
        ArtistPersonId = track.ArtistPersonId,
        AlbumId = track.AlbumId,
        AlbumTitle = track.AlbumTitle,
        Genre = track.Genre,
        CoverUrl = k7ServerService.GetAbsoluteUri(
            track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
        CoverDominantColor = track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?.DominantColor,
        Duration = track.Duration ?? 0,
        UserRating = track.UserRating,
        Bpm = track.Bpm,
        MusicalKey = track.MusicalKey,
        Energy = track.Energy,
        IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
    };

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    internal sealed record TrackViewModel
    {
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistPersonId { get; init; }
        public Guid AlbumId { get; init; }
        public string? AlbumTitle { get; init; }
        public string? Genre { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public double Duration { get; init; }
        public int? UserRating { get; init; }
        public double? Bpm { get; init; }
        public string? MusicalKey { get; init; }
        public double? Energy { get; init; }
        public bool IsPlaying { get; init; }
    }
}
