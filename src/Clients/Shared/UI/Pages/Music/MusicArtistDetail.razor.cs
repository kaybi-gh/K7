using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicArtistDetail
{
    [Parameter]
    public required string Id { get; set; }

    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    private PersonDto? _person;
    private string? _portraitUrl;
    private List<MediaCardViewModel> _albums = [];
    private List<TrackViewModel> _tracks = [];
    private bool _loading = true;
    private bool _isSmallDevice;
    private bool _overviewExpanded;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _person = await k7ServerService.GetPersonAsync(Guid.Parse(Id));

        if (_person is not null)
        {
            _portraitUrl = apiClient.GetAbsoluteUri(
                _person.PortraitPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            var roleMedias = _person.Roles.Select(r => r.Media).Where(m => m is not null);

            _albums = roleMedias
                .OfType<LiteMusicAlbumDto>()
                .DistinctBy(a => a.Id)
                .Select(album => new MediaCardViewModel
                {
                    Id = album.Id.ToString(),
                    Title = album.Title,
                    AdditionalInformations = album.ReleaseDate,
                    PictureUrl = apiClient.GetAbsoluteUri(
                        album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                            .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                })
                .ToList();

            _tracks = roleMedias
                .OfType<LiteMusicTrackDto>()
                .DistinctBy(t => t.Id)
                .OrderBy(t => t.TrackNumber)
                .Select(track => new TrackViewModel
                {
                    Id = track.Id,
                    IndexedFileId = track.IndexedFileId,
                    Title = track.Title ?? S["Untitled"],
                    TrackNumber = track.TrackNumber,
                    AlbumTitle = _albums.FirstOrDefault(a => a.Id == track.AlbumId.ToString())?.Title,
                    CoverUrl = apiClient.GetAbsoluteUri(
                        track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                            .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
                    Duration = track.Duration ?? 0,
                    IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
                })
                .ToList();
        }

        _loading = false;
    }

    private void ScreenResized(Breakpoint breakpoint)
    {
        _isSmallDevice = breakpoint == Breakpoint.Xs;
    }

    private async Task OnTrackClick(TableRowClickEventArgs<TrackViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;

        var queueItems = _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => new AudioQueueItem
        {
            IndexedFileId = t.IndexedFileId!.Value,
            MediaId = t.Id,
            Title = t.Title,
            Artist = _person?.Name,
            AlbumTitle = t.AlbumTitle,
            CoverUrl = t.CoverUrl,
            Duration = t.Duration
        }).ToList();

        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
    }

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
        public int? TrackNumber { get; init; }
        public string? AlbumTitle { get; init; }
        public string? CoverUrl { get; init; }
        public double Duration { get; init; }
        public bool IsPlaying { get; init; }
    }
}
