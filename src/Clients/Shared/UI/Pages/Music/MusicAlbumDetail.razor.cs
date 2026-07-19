using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicAlbumDetail : IDisposable
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
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILibraryService LibraryService { get; set; } = default!;

    [Inject]
    private IFeatureAccessService FeatureAccess { get; set; } = default!;

    [Inject]
    private K7HubClient K7HubClient { get; set; } = default!;

    private MusicAlbumDto? _album;
    private string? _coverUrl;
    private string? _coverDominantColor;
    private List<ArtistInfo> _artists = [];
    private List<TrackViewModel> _tracks = [];
    private SortedDictionary<int, List<TrackViewModel>> _tracksByDisc = [];
    private int _trackCount;
    private double _totalDuration;
    private bool _loading = true;
    private bool _canRate;
    private bool _isAdmin;
    private int? _albumUserRating;
    private Guid? _libraryGroupId;
    private MediaReviewsSection? _reviewsSection;
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
                () => LoadAlbumAsync(isBackgroundRefresh: true),
                () => LoadAlbumAsync(isPicturesRefresh: true));
        }

        await LoadAlbumAsync();
    }

    private async Task LoadAlbumAsync(bool isBackgroundRefresh = false, bool isPicturesRefresh = false)
    {
        if (!isBackgroundRefresh && !isPicturesRefresh)
            _loading = true;

        if (!isBackgroundRefresh && !isPicturesRefresh)
        {
            _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
            var role = await FeatureAccess.GetRoleAsync();
            _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;
        }

        var media = await k7ServerService.GetMediaAsync(
            Guid.Parse(Id),
            bypassCache: isBackgroundRefresh || isPicturesRefresh);
        if (media is MusicAlbumDto album)
        {
            _album = album;
            _albumUserRating = GetUserRating(album.Ratings);

            ApplyAlbumCover(album, isPicturesRefresh ? DateTimeOffset.UtcNow : album.LastMetadataRefreshedAt);

            if (isPicturesRefresh)
            {
                _tracks = _tracks
                    .Select(t => t with { CoverUrl = _coverUrl, CoverDominantColor = _coverDominantColor })
                    .ToList();
                _tracksByDisc = new SortedDictionary<int, List<TrackViewModel>>(
                    _tracks.GroupBy(t => t.DiscNumber).ToDictionary(g => g.Key, g => g.ToList()));

                StateHasChanged();
                return;
            }

            _artists = album.ArtistId.HasValue
                ? [new ArtistInfo(album.ArtistId.Value, album.ArtistName ?? S["UnknownArtist"])]
                : [];

            var artistName = album.ArtistName;

            _tracks = (album.Tracks ?? [])
                .OrderBy(t => t.TrackNumber)
                .Select(t =>
                {
                    var guestCredits = (t.ArtistCredits ?? [])
                        .Where(c => c.IsGuest)
                        .Select(c => new ArtistInfo(c.ArtistId, c.ArtistName))
                        .ToList();

                    return new TrackViewModel
                    {
                        Id = t.Id,
                        IndexedFileId = t.IndexedFileId,
                        RemoteIndexedFileId = t.RemoteIndexedFileId,
                        Title = t.Title ?? S["Untitled"],
                        TrackNumber = t.TrackNumber,
                        ArtistName = artistName,
                        ArtistId = album.ArtistId,
                        FeaturedArtists = guestCredits.Count > 0 ? guestCredits : null,
                        Genre = album.Genres?.FirstOrDefault(),
                        CoverUrl = _coverUrl,
                        CoverDominantColor = _coverDominantColor,
                        Duration = t.Duration ?? 0,
                        DiscNumber = 1,
                        LoudnessLufs = t.LoudnessLufs,
                        FadeInDuration = t.FadeInDuration,
                        FadeOutDuration = t.FadeOutDuration,
                        ReplayGainTrackGain = t.ReplayGainTrackGain,
                        IsPlaying = Audio.CurrentTrack?.MediaId == t.Id
                    };
                })
                .ToList();

            _tracksByDisc = new SortedDictionary<int, List<TrackViewModel>>(
                _tracks.GroupBy(t => t.DiscNumber).ToDictionary(g => g.Key, g => g.ToList()));

            _trackCount = _tracks.Count;
            _totalDuration = _tracks.Sum(t => t.Duration);

            await ResolveLibraryGroupIdAsync();
        }
        else
        {
            _libraryGroupId = null;
        }

        if (!isBackgroundRefresh && !isPicturesRefresh)
            _loading = false;

        if (isBackgroundRefresh && media is MusicAlbumDto)
            Snackbar.Add(S["RefreshMetadataCompleted"], K7Severity.Success);

        StateHasChanged();
    }

    public void Dispose()
    {
        _metadataRefreshWatcher?.Dispose();
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

    private async Task OnTrackClick(K7.Clients.Shared.UI.Components.TableRowClickEventArgs<TrackViewModel> args)
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
            .Where(t => t.IndexedFileId.HasValue || t.RemoteIndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();
    }

    private void NavigateToPrimaryArtist()
    {
        if (_artists.Count > 0)
            NavigationManager.NavigateTo($"/music/artists/{_artists[0].Id}");
    }

    private AudioQueueItem BuildQueueItem(TrackViewModel t)
    {
        return new AudioQueueItem
        {
            IndexedFileId = t.IndexedFileId ?? Guid.Empty,
            RemoteIndexedFileId = t.RemoteIndexedFileId,
            MediaId = t.Id,
            Title = t.Title,
            Artist = t.ArtistName,
            ArtistId = t.ArtistId,
            AlbumTitle = _album?.Title,
            Genre = t.Genre,
            CoverUrl = t.CoverUrl,
            CoverDominantColor = t.CoverDominantColor,
            Duration = t.Duration,
            UserRating = t.UserRating,
            Bpm = t.Bpm,
            MusicalKey = t.MusicalKey,
            Energy = t.Energy,
            LoudnessLufs = t.LoudnessLufs,
            FadeInDuration = t.FadeInDuration,
            FadeOutDuration = t.FadeOutDuration,
            ReplayGainTrackGain = t.ReplayGainTrackGain
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

    private IReadOnlyList<DownloadRequest> GetDownloadRequests()
    {
        return _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(t => new DownloadRequest
            {
                IndexedFileId = t.IndexedFileId!.Value,
                MediaId = t.Id,
                Title = t.Title,
                Artist = t.ArtistName,
                AlbumTitle = _album?.Title,
                CoverUrl = t.CoverUrl,
                MediaType = MediaType.MusicTrack,
                IsCacheItem = false
            })
            .ToList();
    }

    private static string FormatTotalDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours} h {ts.Minutes:00} min";
        return $"{ts.Minutes} min";
    }

    internal sealed record ArtistInfo(Guid Id, string Name);

    private async Task RefreshMetadataAsync()
    {
        if (_album is null) return;
        try
        {
            await k7ServerService.RefreshMediaMetadataAsync(_album.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_album is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _album }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
            if (media is MusicAlbumDto album)
            {
                _album = album;
                StateHasChanged();
            }
        }
    }

    private async Task ResolveLibraryGroupIdAsync()
    {
        var libraryId = _album?.IndexedFiles?.FirstOrDefault()?.LibraryId;
        var groups = await LibraryService.GetLibraryGroupsAsync();
        _libraryGroupId = LibraryGroupBrowseNavigationHelper.ResolveGroupId(
            groups,
            libraryId,
            LibraryMediaType.Music);
    }

    private void NavigateToGenre(string genre)
    {
        if (!_libraryGroupId.HasValue)
            return;

        NavigationManager.NavigateTo(
            LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(
                _libraryGroupId.Value,
                genre: genre,
                mediaType: MediaType.MusicAlbum));
    }

    private async Task OpenReviewDialogAsync()
    {
        if (_album is null)
            return;

        var changed = await MediaReviewDialogHelper.OpenAsync(DialogService, ReviewDialogL, _album.Id, _album.Title);
        if (!changed)
            return;

        var media = await k7ServerService.GetMediaAsync(_album.Id);
        if (media is MusicAlbumDto album)
        {
            _album = album;
            _albumUserRating = GetUserRating(album.Ratings);
        }

        if (_reviewsSection is not null)
            await _reviewsSection.RefreshAsync();
    }

    private void ApplyAlbumCover(MusicAlbumDto album, DateTimeOffset? cacheVersion)
    {
        var coverPicture = album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);
        var coverUri = apiClient.GetAbsoluteUri(
            coverPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
        _coverUrl = MediaPictureUrlHelper.WithCacheBuster(coverUri, cacheVersion);
        _coverDominantColor = coverPicture?.DominantColor;
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)value
            : null;

    internal sealed record TrackViewModel
    {
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public Guid? RemoteIndexedFileId { get; init; }
        public required string Title { get; init; }
        public int? TrackNumber { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistId { get; init; }
        public IReadOnlyList<ArtistInfo>? FeaturedArtists { get; init; }
        public string? Genre { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public double Duration { get; init; }
        public int DiscNumber { get; init; }
        public int? UserRating { get; init; }
        public double? Bpm { get; init; }
        public string? MusicalKey { get; init; }
        public double? Energy { get; init; }
        public double? LoudnessLufs { get; init; }
        public double? FadeInDuration { get; init; }
        public double? FadeOutDuration { get; init; }
        public double? ReplayGainTrackGain { get; init; }
        public bool IsPlaying { get; init; }
    }
}
