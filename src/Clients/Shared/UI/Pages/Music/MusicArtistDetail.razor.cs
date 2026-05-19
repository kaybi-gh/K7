using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicArtistDetail
{
    [Parameter]
    public required string Id { get; set; }

    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    [Inject]
    private IK7DialogService DialogService { get; set; } = default!;

    [Inject]
    private IK7Snackbar Snackbar { get; set; } = default!;

    private MusicArtistDto? _artist;
    private string? _portraitUrl;
    private List<MediaCardViewModel> _albums = [];
    private List<TrackViewModel> _tracks = [];
    private List<LitePersonRoleDto> _members = [];
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));

        if (media is MusicArtistDto artist)
        {
            _artist = artist;

            _portraitUrl = apiClient.GetAbsoluteUri(
                artist.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            var albums = (artist.Albums ?? [])
                .OrderByDescending(a => a.ReleaseDate)
                .ToList();

            _albums = albums
                .Select(album => new MediaCardViewModel
                {
                    Id = album.Id.ToString(),
                    Title = album.Title,
                    AdditionalInformations = album.ReleaseDate?.Year.ToString(),
                    PictureUrl = apiClient.GetAbsoluteUri(
                        (album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                            ?? album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                            .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                })
                .ToList();

            var allTracks = albums
                .SelectMany(a => a.Tracks ?? [])
                .Where(t => t.IndexedFileId.HasValue)
                .DistinctBy(t => t.Id)
                .ToList();

            _tracks = allTracks
                .OrderBy(t => t.TrackNumber)
                .Select(track =>
                {
                    var guestNames = (track.ArtistCredits ?? [])
                        .Where(c => c.IsGuest)
                        .Select(c => c.ArtistName)
                        .ToList();

                    return new TrackViewModel
                    {
                        Id = track.Id,
                        IndexedFileId = track.IndexedFileId,
                        Title = track.Title ?? S["Untitled"],
                        TrackNumber = track.TrackNumber,
                        AlbumTitle = albums.FirstOrDefault(a => a.Tracks?.Any(t => t.Id == track.Id) == true)?.Title,
                        FeaturedArtists = guestNames.Count > 0 ? string.Join(", ", guestNames) : null,
                        CoverUrl = apiClient.GetAbsoluteUri(
                            (track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                                ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                                .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
                        Duration = track.Duration ?? 0,
                        IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
                    };
                })
                .ToList();

            _members = (artist.PersonRoles ?? [])
                .OfType<LiteMusicArtistRoleDto>()
                .Where(r => r.Person is not null)
                .OrderBy(r => r.Order ?? int.MaxValue)
                .Cast<LitePersonRoleDto>()
                .ToList();
        }

        _loading = false;
    }

    private async Task OnTrackClick(K7.Clients.Shared.UI.Components.TableRowClickEventArgs<TrackViewModel> args)
    {
        var track = args.Item;
        if (track is null) return;

        var queueItems = BuildQueueItems();
        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
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

    private Task OpenBiographyDialogAsync()
    {
        if (_artist is null || string.IsNullOrWhiteSpace(_artist.Biography)) return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _artist.Biography },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(_artist.Title ?? L["Biography"], parameters, options);
    }

    private List<AudioQueueItem> BuildQueueItems()
    {
        return _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => new AudioQueueItem
            {
                IndexedFileId = t.IndexedFileId!.Value,
                MediaId = t.Id,
                Title = t.Title,
                Artist = _artist?.Title,
                AlbumTitle = t.AlbumTitle,
                CoverUrl = t.CoverUrl,
                Duration = t.Duration
            }).ToList();
    }

    private IReadOnlyList<DownloadRequest> GetDownloadRequests()
    {
        return _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => new DownloadRequest
            {
                IndexedFileId = t.IndexedFileId!.Value,
                MediaId = t.Id,
                Title = t.Title,
                Artist = _artist?.Title,
                AlbumTitle = t.AlbumTitle,
                CoverUrl = t.CoverUrl,
                MediaType = MediaType.MusicTrack,
                IsCacheItem = false
            })
            .ToList();
    }

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    private async Task RefreshMetadataAsync()
    {
        if (_artist is null) return;
        try
        {
            await k7ServerService.RefreshMediaMetadataAsync(_artist.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    internal sealed record TrackViewModel
    {
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public int? TrackNumber { get; init; }
        public string? AlbumTitle { get; init; }
        public string? FeaturedArtists { get; init; }
        public string? CoverUrl { get; init; }
        public double Duration { get; init; }
        public bool IsPlaying { get; init; }
    }
}
