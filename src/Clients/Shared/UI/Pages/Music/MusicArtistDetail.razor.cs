using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicArtistDetail : IDisposable
{
    [Parameter]
    public required string Id { get; set; }

    [Inject]
    private IAudioPlayerService Audio { get; set; } = default!;

    [Inject]
    private IK7DialogService DialogService { get; set; } = default!;

    [Inject]
    private IK7Snackbar Snackbar { get; set; } = default!;

    [Inject]
    private IServerPreferencesService ServerPreferences { get; set; } = default!;

    [Inject]
    private IFeatureAccessService FeatureAccess { get; set; } = default!;

    [Inject]
    private K7HubClient K7HubClient { get; set; } = default!;

    private MusicArtistDto? _artist;
    private string? _portraitUrl;
    private string? _portraitDominantColor;
    private List<MediaCardViewModel> _albums = [];
    private List<MediaCardViewModel> _similarArtists = [];
    private List<TrackViewModel> _topTracks = [];
    private List<TrackViewModel> _tracks = [];
    private List<LitePersonRoleDto> _members = [];
    private bool _loading = true;
    private bool _canRate;
    private int? _artistUserRating;
    private MediaMetadataRefreshWatcher? _metadataRefreshWatcher;

    protected override void OnInitialized()
    {
        _metadataRefreshWatcher = new MediaMetadataRefreshWatcher(K7HubClient, InvokeAsync);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Guid.TryParse(Id, out var mediaId))
        {
            _metadataRefreshWatcher?.Watch(
                mediaId,
                () => LoadArtistAsync(isBackgroundRefresh: true),
                () => LoadArtistAsync(isPicturesRefresh: true));
        }

        await LoadArtistAsync();
    }

    private async Task LoadArtistAsync(bool isBackgroundRefresh = false, bool isPicturesRefresh = false)
    {
        if (!isBackgroundRefresh && !isPicturesRefresh)
            _loading = true;

        if (!isBackgroundRefresh && !isPicturesRefresh)
            _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);

        var media = await k7ServerService.GetMediaAsync(
            Guid.Parse(Id),
            bypassCache: isBackgroundRefresh || isPicturesRefresh);

        if (media is MusicArtistDto artist)
        {
            _artist = artist;
            _artistUserRating = GetUserRating(artist.Ratings);

            ApplyArtistPortrait(artist, isPicturesRefresh ? DateTimeOffset.UtcNow : artist.LastMetadataRefreshedAt);

            if (isPicturesRefresh)
            {
                var pictureCacheVersion = DateTimeOffset.UtcNow;
                _albums = (artist.Albums ?? [])
                    .OrderByDescending(a => a.ReleaseDate)
                    .Select(album => new MediaCardViewModel
                    {
                        Id = album.Id.ToString(),
                        Kind = MediaCardKind.Cover,
                        MediaType = MediaType.MusicAlbum,
                        Title = album.Title,
                        AdditionalInformations = album.ReleaseDate?.Year.ToString(),
                        PictureUrl = MediaPictureUrlHelper.WithCacheBuster(
                            apiClient.GetAbsoluteUri(
                                (album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                                    ?? album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
                            pictureCacheVersion)
                    })
                    .ToList();

                StateHasChanged();
                return;
            }

            var albums = (artist.Albums ?? [])
                .OrderByDescending(a => a.ReleaseDate)
                .ToList();

            _albums = albums
                .Select(album => new MediaCardViewModel
                {
                    Id = album.Id.ToString(),
                    Kind = MediaCardKind.Cover,
                    MediaType = MediaType.MusicAlbum,
                    Title = album.Title,
                    AdditionalInformations = album.ReleaseDate?.Year.ToString(),
                    PictureUrl = apiClient.GetAbsoluteUri(
                        (album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                            ?? album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                            .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                })
                .ToList();

            var topTracks = await k7ServerService.GetArtistTopTracksAsync(Guid.Parse(Id));
            _topTracks = AssignRanks(MapTracks(topTracks));

            var allTracksResult = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
            {
                MediaTypes = [MediaType.MusicTrack],
                ArtistIds = [Guid.Parse(Id)],
                OrderBy = [MediaOrderingOption.TitleAsc],
                PageNumber = 1,
                PageSize = 500
            });

            _tracks = (allTracksResult?.Items ?? [])
                .OfType<LiteMusicTrackDto>()
                .Select(MapTrack)
                .ToList();

            if (_topTracks.Count == 0 && _tracks.Count > 0)
                _topTracks = AssignRanks(_tracks.Take(10));

            _members = (artist.PersonRoles ?? [])
                .OfType<LiteMusicArtistRoleDto>()
                .Where(r => r.Person is not null)
                .OrderBy(r => r.Order ?? int.MaxValue)
                .Cast<LitePersonRoleDto>()
                .ToList();

            await LoadSimilarArtistsAsync(artist.Id);
        }

        if (!isBackgroundRefresh && !isPicturesRefresh)
            _loading = false;

        if (isBackgroundRefresh && media is MusicArtistDto)
            Snackbar.Add(S["RefreshMetadataCompleted"], K7Severity.Success);

        StateHasChanged();
    }

    public void Dispose()
    {
        _metadataRefreshWatcher?.Dispose();
    }

    private void ApplyArtistPortrait(MusicArtistDto artist, DateTimeOffset? cacheVersion)
    {
        var portraitPicture = artist.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
            ?? artist.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? artist.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Portrait);
        var portraitUri = apiClient.GetAbsoluteUri(
            portraitPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
        _portraitUrl = MediaPictureUrlHelper.WithCacheBuster(portraitUri, cacheVersion);
        _portraitDominantColor = portraitPicture?.DominantColor;
    }

    private async Task LoadSimilarArtistsAsync(Guid artistId)
    {
        _similarArtists = [];
        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            if (!status.IsAvailable)
                return;

            var similar = await k7ServerService.GetSimilarMusicArtistsAsync(artistId);
            _similarArtists = similar
                .Select(MapArtistCard)
                .ToList();
        }
        catch
        {
            _similarArtists = [];
        }
    }

    private MediaCardViewModel MapArtistCard(LiteMusicArtistDto artist) => new()
    {
        Id = artist.Id.ToString(),
        Kind = MediaCardKind.Cover,
        MediaType = MediaType.MusicArtist,
        Title = artist.Title,
        PictureUrl = apiClient.GetAbsoluteUri(
            (artist.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                ?? artist.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover))?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
    };

    private List<TrackViewModel> MapTracks(IEnumerable<LiteMusicTrackDto> tracks) =>
        tracks.Select(MapTrack).ToList();

    private static List<TrackViewModel> AssignRanks(IEnumerable<TrackViewModel> tracks) =>
        tracks.Select((track, index) => track with { Rank = index + 1 }).ToList();

    private TrackViewModel MapTrack(LiteMusicTrackDto track) => new()
    {
        Id = track.Id,
        IndexedFileId = track.IndexedFileId,
        Title = track.Title ?? S["Untitled"],
        AlbumTitle = track.AlbumTitle,
        ArtistId = track.ArtistId ?? _artist?.Id,
        Genre = track.Genre,
        CoverUrl = apiClient.GetAbsoluteUri(
            (track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
                ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster))?
                .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri,
        Duration = track.Duration ?? 0,
        UserRating = track.UserRating,
        IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
    };

    private AudioQueueItem BuildQueueItem(TrackViewModel track) => new()
    {
        IndexedFileId = track.IndexedFileId ?? Guid.Empty,
        MediaId = track.Id,
        Title = track.Title,
        Artist = _artist?.Title,
        ArtistId = track.ArtistId,
        AlbumTitle = track.AlbumTitle,
        Genre = track.Genre,
        CoverUrl = track.CoverUrl,
        Duration = track.Duration,
        UserRating = track.UserRating
    };

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

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_artist is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _artist }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
            if (media is MusicArtistDto artist)
            {
                _artist = artist;
                StateHasChanged();
            }
        }
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)value
            : null;

    internal sealed record TrackViewModel
    {
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public int? Rank { get; init; }
        public string? AlbumTitle { get; init; }
        public Guid? ArtistId { get; init; }
        public string? Genre { get; init; }
        public string? CoverUrl { get; init; }
        public double Duration { get; init; }
        public int? UserRating { get; init; }
        public bool IsPlaying { get; init; }
    }
}
