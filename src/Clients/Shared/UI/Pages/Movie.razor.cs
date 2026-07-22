using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Enums;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class Movie : IAsyncDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IFederationService FederationService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;

    [Parameter] public required string Id { get; set; }

    private bool isLoading { get; set; } = true;
    private static MovieDto? _movie;
    private static MediaCardViewModel? _mediaCard;
    private string? _backdropUrl;
    private string? _dominantColor;
    private string? _logoUrl;
    private string? _posterSmallUrl;
    private bool _overviewExpanded;
    private IndexedFileDto? _selectedFile;
    private RemoteIndexedFileDto? _selectedRemoteFile;
    private AudioFileTrackDto? _selectedAudioFileTrack;
    private SubtitleFileTrackDto? _selectedSubtitleFileTrack;
    private List<MediaCardViewModel> _similarMedia = [];
    private string? _previousId;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _canRate;
    private bool _isAdmin;
    private bool _isTv;
    private ElementReference _tvScrollRoot;
    private bool _tvScrollInitialized;
    private bool _initialFocusApplied;
    private Guid? _libraryGroupId;
    private MediaReviewsSection? _reviewsSection;
    private int? _movieUserRating;
    private MediaMetadataRefreshWatcher? _metadataRefreshWatcher;

    protected override void OnInitialized()
    {
        _metadataRefreshWatcher = new MediaMetadataRefreshWatcher(K7HubClient, InvokeAsync);
    }

    private bool HasTvBelowContent =>
        (_movie?.PersonRoles?.Count ?? 0) > 0 || _similarMedia.Count > 0;

    private string? GetLogoUrl() => _logoUrl;

    protected override async Task OnParametersSetAsync()
    {
        if (_previousId is null)
        {
            _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
            (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
            _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);
            _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
        }

        if (_previousId == Id) return;
        _previousId = Id;

        if (Guid.TryParse(Id, out var mediaId))
        {
            _metadataRefreshWatcher?.Watch(
                mediaId,
                () => LoadMovieAsync(isBackgroundRefresh: true),
                () => LoadMovieAsync(isPicturesRefresh: true));
        }

        await LoadMovieAsync();
    }

    private async Task LoadMovieAsync(bool isBackgroundRefresh = false, bool isPicturesRefresh = false)
    {
        if (!isBackgroundRefresh && !isPicturesRefresh)
        {
            _tvScrollInitialized = false;
            _initialFocusApplied = false;
            _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
            isLoading = true;
            _similarMedia = [];
        }

        var movie = await k7ServerService.GetMovieAsync(
            Guid.Parse(Id),
            bypassCache: isBackgroundRefresh || isPicturesRefresh);
        if (movie != null)
        {
            _movie = movie;
            _movieUserRating = GetUserRating(_movie.Ratings);

            var cacheVersion = isPicturesRefresh
                ? DateTimeOffset.UtcNow
                : _movie.LastMetadataRefreshedAt;

            var backdropPicture = _movie.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Backdrop);
            var backdropUri = apiClient.GetAbsoluteUri(
                backdropPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _backdropUrl = MediaPictureUrlHelper.WithCacheBuster(backdropUri, cacheVersion);
            _dominantColor = backdropPicture?.DominantColor;

            var logoUri = apiClient.GetAbsoluteUri(
                _movie.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Logo)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _logoUrl = MediaPictureUrlHelper.WithCacheBuster(logoUri, cacheVersion);

            var posterUri = apiClient.GetAbsoluteUri(
                _movie.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
            _posterSmallUrl = MediaPictureUrlHelper.WithCacheBuster(posterUri, cacheVersion);

            _mediaCard = new MediaCardViewModel()
            {
                Id = _movie.Id.ToString(),
                Title = _movie.Title,
                PictureUrl = _posterSmallUrl
            };
            
            _selectedFile = _movie.IndexedFiles?.FirstOrDefault();
            _selectedRemoteFile = _selectedFile is null
                ? _movie.RemoteIndexedFiles?.FirstOrDefault()
                : null;
            if (_selectedFile?.FileMetadata is VideoFileMetadataDto vMeta)
            {
                _selectedAudioFileTrack = vMeta.AudioTracks?.FirstOrDefault(x => x.IsDefault) ?? vMeta.AudioTracks?.FirstOrDefault();
                _selectedSubtitleFileTrack = vMeta.SubtitleTracks?.FirstOrDefault(x => x.IsDefault);
            }

            await ResolveLibraryGroupIdAsync();

            if (!isBackgroundRefresh && !isPicturesRefresh)
            {
                await ThemeSongPlaybackHelper.TryStartAsync(
                    movie.Id,
                    movie.HasThemeSong,
                    k7ServerService,
                    UserPreferencesService,
                    AmbientThemeService,
                    AudioPlayerService,
                    DeviceStorageService);
            }
        }
        else
        {
            _libraryGroupId = null;
            await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);
        }

        if (!isBackgroundRefresh && !isPicturesRefresh)
            isLoading = false;

        if (!isPicturesRefresh)
            LoadSimilarMediaAsync().FireAndForget();

        if (isBackgroundRefresh && movie is not null)
            Snackbar.Add(S["RefreshMetadataCompleted"], K7Severity.Success);

        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isTv && !isLoading && _movie is not null && HasTvBelowContent)
        {
            if (!_tvScrollInitialized)
            {
                await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.init", _tvScrollRoot);
                _tvScrollInitialized = true;
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.sync", _tvScrollRoot);
            }
        }

        if (!_initialFocusApplied && !isLoading && _movie is not null)
        {
            _initialFocusApplied = true;
            try
            {
                await SpatialNav.FocusFirstAsync("[data-initial-focus]");
            }
            catch (InvalidOperationException) { }
        }
    }

    private void ToggleOverview()
    {
        _overviewExpanded = !_overviewExpanded;
    }

    private async Task PlayAsync()
    {
        if (_movie is null || (!HasPlayableFiles()))
        {
            return;
        }

        await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);

        // Remote file playback (federation)
        if (_selectedRemoteFile is not null)
        {
            await PlayRemoteFileAsync(_selectedRemoteFile);
            return;
        }

        if (_selectedFile is null)
        {
            return;
        }

        var indexedFileId = _selectedFile.Id;
        if (_selectedFile.FileMetadata is not VideoFileMetadataDto videoMetadata)
        {
            return;
        }
        
        var audioTracks = videoMetadata.AudioTracks;
        var subtitleTracks = videoMetadata.SubtitleTracks;
        var audioTrackIndex = _selectedAudioFileTrack?.Index;
        var subtitleTrackIndex = _selectedSubtitleFileTrack?.Index;
        var videoResolution = videoMetadata.VideoResolution;
        var thumbnailsUrl = videoMetadata.Thumbnails?.Uri?.ToString();

        PlaybackProgressTracker.StartTracking(_movie.Id, await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress), indexedFileId: indexedFileId);

        var coverUrl = apiClient.GetAbsoluteUri(_movie.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Poster)?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

        double? startPosition = null;
        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && _movie.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            startPosition = _movie.UserState.LastPlaybackPosition;
        }

        await PlayerService.PlayIndexedFileAsync(indexedFileId, audioTracks ?? [], subtitleTracks, audioTrackIndex, subtitleTrackIndex, videoResolution, thumbnailsUrl, _movie.Id, _movie.Title, coverUrl, startPosition, videoMetadata.Chapters);
    }

    private bool HasPlayableFiles()
    {
        if (_movie is null) return false;
        return (_movie.IndexedFiles is { Count: > 0 }) || (_movie.RemoteIndexedFiles is { Count: > 0 });
    }

    private async Task PlayRemoteFileAsync(RemoteIndexedFileDto remoteFile)
    {
        if (_movie is null) return;

        PlaybackProgressTracker.StartTracking(_movie.Id, await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress));

        var coverUrl = apiClient.GetAbsoluteUri(_movie.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Poster)?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

        var details = await FederationService.GetRemoteFileDetailsAsync(remoteFile.Id);
        var videoMetadata = details?.FileMetadata as VideoFileMetadataDto;

        double? startPosition = null;
        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && _movie.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            startPosition = _movie.UserState.LastPlaybackPosition;
        }

        await PlayerService.PlayRemoteIndexedFileAsync(
            remoteFile.Id,
            videoMetadata?.AudioTracks ?? [],
            videoMetadata?.SubtitleTracks,
            _selectedAudioFileTrack?.Index ?? videoMetadata?.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            _selectedSubtitleFileTrack?.Index ?? videoMetadata?.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata?.VideoResolution,
            _movie.Id,
            _movie.Title,
            coverUrl,
            startPosition);
    }

    private async Task OpenPlaybackOptionsAsync()
    {
        if (_movie is null) return;

        var movieForDialog = _movie;

        // For remote-only movies, fetch file details from the peer
        if (_movie.IndexedFiles is not { Count: > 0 } && _movie.RemoteIndexedFiles is { Count: > 0 })
        {
            var remoteFile = _selectedRemoteFile ?? _movie.RemoteIndexedFiles.First();
            var details = await FederationService.GetRemoteFileDetailsAsync(remoteFile.Id);
            if (details is null) return;

            movieForDialog = _movie with { IndexedFiles = [details] };
            _selectedFile = details;
        }

        if (movieForDialog.IndexedFiles is not { Count: > 0 }) return;

        var parameters = new K7DialogParameters<PlaybackOptionsDialog>
        {
            { x => x.Movie, movieForDialog },
            { x => x.InitialFileId, _selectedFile?.Id }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        
        var dialog = await DialogService.ShowAsync<PlaybackOptionsDialog>(L["TracksSelection"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data is PlaybackOptionsResult optionsResult)
        {
            _selectedFile = optionsResult.SelectedFile;
            _selectedAudioFileTrack = optionsResult.AudioTrack;
            _selectedSubtitleFileTrack = optionsResult.SubtitleTrack;
            
            await PlayAsync();
        }
    }

    private async Task OpenMediaReIdentifyDialogAsync()
    {
        if (_movie == null) return;

        var (searchQuery, searchYear) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            _movie.IndexedFiles,
            MediaType.Movie,
            fallbackQuery: _movie.Title,
            fallbackYear: _movie.ReleaseDate?.Year);

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _movie.Id },
            { x => x.InitialSearchQuery, searchQuery },
            { x => x.InitialSearchYear, searchYear },
            { x => x.MediaType, MediaType.Movie },
            { x => x.LibraryId, GetLibraryIdForReIdentify() }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], K7Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task OpenFileReIdentifyDialogAsync(Guid indexedFileId)
    {
        var (searchQuery, searchYear) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            _movie?.IndexedFiles,
            MediaType.Movie,
            preferredIndexedFileId: indexedFileId,
            fallbackQuery: _movie?.Title,
            fallbackYear: _movie?.ReleaseDate?.Year);

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.IndexedFileId, indexedFileId },
            { x => x.InitialSearchQuery, searchQuery },
            { x => x.InitialSearchYear, searchYear },
            { x => x.MediaType, MediaType.Movie },
            { x => x.LibraryId, GetLibraryIdForReIdentify(indexedFileId) }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyFileDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled)
        {
            Snackbar.Add(L["ReIdentifyFileSent"], K7Severity.Success);
            NavigationManager.NavigateTo("/");
        }
    }

    private async Task OpenIndexedFilesDialogAsync()
    {
        if (_movie == null) return;

        var parameters = new K7DialogParameters<IndexedFilesDialog>
        {
            { x => x.Media, _movie },
            { x => x.OnReIdentifyFile, EventCallback.Factory.Create<Guid>(this, OpenFileReIdentifyDialogAsync) }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<IndexedFilesDialog>(L["IndexedVersions"], parameters, options);
    }

    private Task OpenSynopsisDialogAsync()
    {
        if (_movie == null || string.IsNullOrWhiteSpace(_movie.Overview)) return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _movie.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private async Task RefreshMetadataAsync()
    {
        if (_movie is null) return;

        try
        {
            await k7ServerService.RefreshMediaMetadataAsync(_movie.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenTrailerAsync()
    {
        if (_movie?.Trailers is not { Count: > 0 }) return;

        await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);

        var trailer = _movie.Trailers.FirstOrDefault(t => t.Type == "Trailer") ?? _movie.Trailers[0];
        var parameters = new K7DialogParameters<TrailerDialog>
        {
            { x => x.TrailerKey, trailer.Key },
            { x => x.TrailerSite, trailer.Site ?? "YouTube" }
        };
        var options = new K7DialogOptions { FullScreen = true, CloseOnEscapeKey = true, CloseButton = true };
        await DialogService.ShowAsync<TrailerDialog>(trailer.Name ?? L["Trailer"], parameters, options);
    }

    private async Task LoadSimilarMediaAsync()
    {
        if (_movie is null) return;

        try
        {
            var similar = await k7ServerService.GetSimilarMediaAsync(_movie.Id);
            _similarMedia = [];
            foreach (var media in similar)
            {
                if (media.ToCardViewModel(apiClient, FormatSeasonNumber) is { } vm)
                    _similarMedia.Add(vm);
            }
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // Non-critical - silently ignore if similar media fails
        }
    }

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_movie is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _movie }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            _movie = await k7ServerService.GetMovieAsync(Guid.Parse(Id));
            StateHasChanged();
        }
    }

    private async Task ToggleWatchStateAsync()
    {
        if (_movie is null)
            return;

        var watched = _movie.UserState?.IsCompleted != true;
        var success = await WatchStateActions.ApplyAsync(
            k7ServerService,
            CacheStore,
            DialogService,
            Snackbar,
            S,
            _movie.Id,
            watched,
            WatchStateScope.Item);

        if (!success)
            return;

        _movie = await k7ServerService.GetMovieAsync(_movie.Id);
        StateHasChanged();
    }

    private string FormatSeasonNumber(int seasonNumber) => string.Format(S["SeasonNumber"], seasonNumber);

    private static string GetSimilarMediaHref(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Serie => $"/series/{item.Id}",
        _ => $"/movies/{item.Id}"
    };

    private async Task ExcludeSimilarForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            _similarMedia.RemoveAll(m => m.Id == item.Id || m.ParentId == item.Id);
    }

    private Task ExcludeSimilarForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);

    private Guid? GetLibraryIdForReIdentify(Guid? indexedFileId = null)
    {
        if (_movie?.LibraryId is { } libraryId)
            return libraryId;

        if (_movie?.IndexedFiles is not { Count: > 0 })
            return null;

        if (indexedFileId.HasValue)
            return _movie.IndexedFiles.FirstOrDefault(f => f.Id == indexedFileId)?.LibraryId;

        return _movie.IndexedFiles.First().LibraryId;
    }

    private async Task ResolveLibraryGroupIdAsync()
    {
        var libraryId = GetLibraryIdForReIdentify();
        var groups = await LibraryService.GetLibraryGroupsAsync();
        _libraryGroupId = LibraryGroupBrowseNavigationHelper.ResolveGroupId(
            groups,
            libraryId,
            LibraryMediaType.Movie);
    }

    private void NavigateToGenre(string genre)
    {
        if (!_libraryGroupId.HasValue)
            return;

        NavigationManager.NavigateTo(
            LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(_libraryGroupId.Value, genre: genre));
    }

    private void NavigateToStudio(string studio)
    {
        if (!_libraryGroupId.HasValue)
            return;

        NavigationManager.NavigateTo(
            LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(_libraryGroupId.Value, studio: studio));
    }

    private async Task OpenReviewDialogAsync()
    {
        if (_movie is null)
            return;

        var changed = await MediaReviewDialogHelper.OpenAsync(DialogService, ReviewDialogL, _movie.Id, _movie.Title);
        if (!changed)
            return;

        _movie = await k7ServerService.GetMovieAsync(_movie.Id);
        if (_movie is not null)
            _movieUserRating = GetUserRating(_movie.Ratings);

        if (_reviewsSection is not null)
            await _reviewsSection.RefreshAsync();
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)Math.Round(value)
            : null;

    public async ValueTask DisposeAsync()
    {
        await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);
        _metadataRefreshWatcher?.Dispose();

        if (_tvScrollInitialized)
            await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.dispose", _tvScrollRoot);
    }
}
