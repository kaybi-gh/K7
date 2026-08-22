using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
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

namespace K7.Clients.Shared.UI.Pages;

public partial class Serie : IAsyncDisposable
{
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;
    [Parameter]
    public required string Id { get; set; }

    private SerieDto? _serie;
    private string? _backdropUrl;
    private string? _backdropHighResUrl;
    private string? _dominantColor;
    private string? _logoUrl;
    private List<LiteSerieSeasonDto> _seasons = [];
    private List<MediaCardViewModel> _similarMedia = [];
    private bool _loading = true;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _canRate;
    private bool _isAdmin;
    private bool _permissionsLoaded;
    private bool _isTv;
    private int? _serieUserRating;
    private ElementReference _tvScrollRoot;
    private bool _tvScrollInitialized;
    private Guid? _libraryGroupId;
    private List<SerieStudioNetworkChip> _studioNetworkChips = [];
    private MediaReviewsSection? _reviewsSection;
    private LiteSerieEpisodeDto? _resumeEpisode;
    private bool _initialFocusApplied;
    private MediaMetadataRefreshWatcher? _metadataRefreshWatcher;
    private Timer? _indexedFilesDebounceTimer;
    private DebouncedActionRunner? _progressRefreshRunner;

    protected override void OnInitialized()
    {
        _metadataRefreshWatcher = new MediaMetadataRefreshWatcher(K7HubClient, InvokeAsync);
        _progressRefreshRunner = new DebouncedActionRunner(
            RefreshProgressFromHubAsync,
            InvokeAsync,
            delayMs: 800);
        K7HubClient.MediaIndexedFilesUpdated += OnMediaIndexedFilesUpdated;
        K7HubClient.ProgressUpdated += OnProgressUpdated;
    }

    private void OnMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId)
    {
        // Episode probe events carry the episode id, which this page does not track:
        // match on the serie itself or its library, debounce (probes come in bursts
        // during imports), then silently reload so playback state stays fresh.
        if (_serie is null || (mediaId != _serie.Id && libraryId != _serie.LibraryId))
            return;

        _indexedFilesDebounceTimer?.Dispose();
        _indexedFilesDebounceTimer = new Timer(
            _ => InvokeAsync(() => LoadSerieAsync(isPicturesRefresh: true)).FireAndForget(),
            null,
            TimeSpan.FromMilliseconds(500),
            Timeout.InfiniteTimeSpan);
    }

    private void OnProgressUpdated(Guid mediaId, double progressPercentage, bool isCompleted, MediaType mediaType)
    {
        if (_serie is null || !_canTrackProgress)
            return;

        if (mediaType is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist)
            return;

        // Ignore self-echo while playing an episode of this serie on this client.
        if (PlaybackProgressTracker.CurrentSerieId == _serie.Id
            || PlaybackProgressTracker.CurrentMediaId == mediaId)
            return;

        // Episode ticks use episode ids; only react to this serie, its seasons, or the
        // current resume episode (not every SerieEpisode in the household).
        var relevant = mediaId == _serie.Id
            || _resumeEpisode?.Id == mediaId
            || _seasons.Any(s => s.Id == mediaId);

        if (!relevant)
            return;

        _progressRefreshRunner?.Schedule();
    }

    private async Task RefreshProgressFromHubAsync()
    {
        if (_serie is null || !_canTrackProgress)
            return;

        await ReloadSerieAsync();
        await RefreshResumeEpisodeAsync();
        StateHasChanged();
    }

    private bool HasTvBelowContent =>
        (_serie?.PersonRoles?.Count ?? 0) > 0 || _similarMedia.Count > 0;

    private bool HasBelowContent =>
        _seasons.Count > 0 || HasTvBelowContent;

    private bool HasTvScrollContent =>
        HasBelowContent;

    protected override async Task OnParametersSetAsync()
    {
        if (!_permissionsLoaded)
        {
            _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
            (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
            _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);
            _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);
            _permissionsLoaded = true;
        }

        if (Guid.TryParse(Id, out var mediaId))
        {
            _metadataRefreshWatcher?.Watch(
                mediaId,
                () => LoadSerieAsync(isBackgroundRefresh: true),
                () => LoadSerieAsync(isPicturesRefresh: true));
        }

        await LoadSerieAsync();
    }

    private async Task LoadSerieAsync(bool isBackgroundRefresh = false, bool isPicturesRefresh = false)
    {
        if (!isBackgroundRefresh && !isPicturesRefresh)
        {
            _loading = true;
            _tvScrollInitialized = false;
            _initialFocusApplied = false;
            _resumeEpisode = null;
            _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        }

        if (!Guid.TryParse(Id, out var mediaId))
        {
            _serie = null;
            if (!isBackgroundRefresh && !isPicturesRefresh)
                _loading = false;

            StateHasChanged();
            return;
        }

        var media = await k7ServerService.GetMediaAsync(
            mediaId,
            bypassCache: isBackgroundRefresh || isPicturesRefresh);
        if (media is SerieDto serie)
        {
            _serie = serie;
            _serieUserRating = GetUserRating(serie.Ratings);

            var cacheVersion = isPicturesRefresh
                ? DateTimeOffset.UtcNow
                : serie.LastMetadataRefreshedAt;

            var backdropPicture = serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
            (_backdropUrl, _backdropHighResUrl) = MetadataPictureDisplayHelper.ResolveAdaptiveBackdropUrls(
                backdropPicture,
                apiClient,
                cacheVersion);

            _dominantColor = backdropPicture?.DominantColor;

            var logoUri = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Logo)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _logoUrl = MediaPictureUrlHelper.WithCacheBuster(logoUri, cacheVersion);

            _seasons = (serie.Seasons ?? [])
                .Where(s => s.EpisodeCount > 0)
                .OrderBy(s => s.SeasonNumber == 0 ? int.MaxValue : s.SeasonNumber)
                .ToList();

            BuildStudioNetworkChips();
            await ResolveLibraryGroupIdAsync();

            if (!isBackgroundRefresh && !isPicturesRefresh)
            {
                await ThemeSongPlaybackHelper.TryStartAsync(
                    serie.Id,
                    serie.HasThemeSong,
                    k7ServerService,
                    UserPreferencesService,
                    AmbientThemeService,
                    AudioPlayerService,
                    DeviceStorageService);
            }
        }
        else
        {
            _serie = null;
            _libraryGroupId = null;
            _studioNetworkChips = [];
            await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);
        }

        if (!isBackgroundRefresh && !isPicturesRefresh)
            _loading = false;

        if (!isPicturesRefresh)
            LoadSimilarMediaAsync().FireAndForget();

        if (isBackgroundRefresh && media is SerieDto)
            Snackbar.Add(S["RefreshMetadataCompleted"], K7Severity.Success);

        if (!isPicturesRefresh && _canTrackProgress && _seasons.Count > 0)
            await RefreshResumeEpisodeAsync();
        else if (!_canTrackProgress)
            _resumeEpisode = null;

        StateHasChanged();
    }

    private async Task RefreshResumeEpisodeAsync()
    {
        var episode = await SeriePlaybackHelper.ResolveEpisodeToPlayAsync(k7ServerService, _seasons);
        _resumeEpisode = SeriePlaybackHelper.IsInProgress(episode) ? episode : null;
    }

    private bool CanResumePlayback =>
        _canTrackProgress && SeriePlaybackHelper.IsInProgress(_resumeEpisode);

    private string PrimaryPlayLabel
    {
        get
        {
            if (!CanResumePlayback)
                return S["Play"];

            var position = PlaybackPositionFormatter.TryFormat(_resumeEpisode?.UserState?.LastPlaybackPosition ?? 0);
            if (position is not null)
                return string.Format(S["ResumeAtTime"], position);

            return S["Resume"];
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_loading && _serie is not null && HasTvScrollContent)
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

        if (!_initialFocusApplied && !_loading && _serie is not null && _isTv)
        {
            _initialFocusApplied = true;
            try
            {
                await SpatialNav.FocusFirstAsync(".serie-actions-play[data-initial-focus], [data-tv-scroll-zone='actions'] [data-initial-focus]");
            }
            catch (InvalidOperationException) { }
        }
    }

    private string? GetSeasonPosterUrl(LiteSerieSeasonDto season)
    {
        var picture = LiteMediaThumbnailHelper.ResolvePicture(season);
        return apiClient.GetAbsoluteUri(
            picture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    private void NavigateToSeason(LiteSerieSeasonDto season)
    {
        NavigationManager.NavigateTo($"/series/{Id}/seasons/{season.SeasonNumber}");
    }

    private async Task WatchAsync(bool fromBeginning = false)
    {
        if (_seasons.Count == 0)
            return;

        var episode = _resumeEpisode
            ?? await SeriePlaybackHelper.ResolveEpisodeToPlayAsync(k7ServerService, _seasons);
        if (episode is null)
            return;

        // Play from beginning always restarts the in-progress episode (or the next episode) at 0.
        if (!fromBeginning && !SeriePlaybackHelper.IsInProgress(episode))
            fromBeginning = false;

        await ThemeSongPlaybackHelper.InterruptAsync(AmbientThemeService);

        var result = await SeriePlaybackHelper.PlayEpisodeAsync(
            episode,
            Guid.Parse(Id),
            k7ServerService,
            PlayerService,
            PlaybackProgressTracker,
            FeatureAccess,
            FederationService,
            apiClient,
            fromBeginning: fromBeginning);

        if (result == EpisodePlaybackResult.AwaitingProbe)
        {
            // The file is indexed but not probed yet: tell the user instead of ignoring the click.
            Snackbar.Add(S["MediaPreparingPlayback"], K7Severity.Info);
        }
    }

    private async Task OpenMediaReIdentifyDialogAsync()
    {
        if (_serie is null) return;

        // Serie.IndexedFiles is empty (files live on episodes): fetch a sample episode for path / search defaults.
        var sampleFiles = await ResolveSerieSampleIndexedFilesAsync();

        var (searchQuery, searchYear) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            sampleFiles,
            MediaType.Serie,
            fallbackQuery: _serie.Title,
            fallbackYear: _serie.ReleaseDate?.Year);

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _serie.Id },
            { x => x.InitialSearchQuery, searchQuery },
            { x => x.InitialSearchYear, searchYear },
            { x => x.MediaType, MediaType.Serie },
            { x => x.LibraryId, GetLibraryIdForReIdentify() },
            { x => x.SourcePath, ReIdentifySearchDefaultsHelper.ResolveSourcePath(sampleFiles, MediaType.Serie) }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], K7Severity.Success);
            await NavigationHistoryHelper.NavigateBackOrHomeAsync(JSRuntime, NavigationManager);
        }
    }

    /// <summary>
    /// Files are attached to episodes, not the serie. Load the first episode with a local file
    /// so the re-identify dialog can show the series folder / sample path.
    /// </summary>
    private async Task<IReadOnlyList<IndexedFileDto>?> ResolveSerieSampleIndexedFilesAsync()
    {
        if (_serie?.IndexedFiles is { Count: > 0 } existing)
            return existing;

        var orderedSeasons = _seasons
            .OrderBy(s => s.SeasonNumber == 0 ? int.MaxValue : s.SeasonNumber);

        try
        {
            foreach (var season in orderedSeasons)
            {
                if (await k7ServerService.GetMediaAsync(season.Id) is not SerieSeasonDto seasonDto)
                    continue;

                var episodeLite = (seasonDto.Episodes ?? [])
                    .Where(e => e.IndexedFileId.HasValue)
                    .OrderBy(e => e.EpisodeNumber)
                    .FirstOrDefault();
                if (episodeLite is null)
                    continue;

                if (await k7ServerService.GetMediaAsync(episodeLite.Id) is SerieEpisodeDto episode
                    && episode.IndexedFiles is { Count: > 0 } files)
                {
                    return files;
                }
            }
        }
        catch
        {
            // Non-critical: dialog still opens without source path context.
        }

        return _serie?.IndexedFiles;
    }

    private async Task RefreshMetadataAsync()
    {
        if (_serie is null) return;

        try
        {
            await k7ServerService.RefreshMediaMetadataAsync(_serie.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_serie is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _serie }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
            if (media is SerieDto serie)
            {
                _serie = serie;
                StateHasChanged();
            }
        }
    }

    private Task OpenSynopsisDialogAsync()
    {
        if (_serie is null || string.IsNullOrWhiteSpace(_serie.Overview)) return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _serie.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private async Task OpenTrailerAsync()
    {
        if (_serie?.Trailers is not { Count: > 0 }) return;

        await ThemeSongPlaybackHelper.InterruptAsync(AmbientThemeService);

        var trailer = _serie.Trailers.FirstOrDefault(t => t.Type == "Trailer") ?? _serie.Trailers[0];
        var parameters = new K7DialogParameters<TrailerDialog>
        {
            { x => x.TrailerKey, trailer.Key },
            { x => x.TrailerSite, trailer.Site ?? "YouTube" }
        };
        var options = new K7DialogOptions { FullScreen = true, CloseOnEscapeKey = true, CloseButton = true };
        await DialogService.ShowAsync<TrailerDialog>(trailer.Name ?? L["Trailer"], parameters, options);
    }

    private void NavigateToStudio(string studio)
    {
        if (!_libraryGroupId.HasValue)
            return;

        NavigationManager.NavigateTo(
            LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(_libraryGroupId.Value, studio: studio));
    }

    private void NavigateToNetwork(string network)
    {
        if (!_libraryGroupId.HasValue)
            return;

        NavigationManager.NavigateTo(
            LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(_libraryGroupId.Value, network: network));
    }

    private void NavigateToStudioNetworkChip(SerieStudioNetworkChip chip)
    {
        if (chip.IsNetwork)
            NavigateToNetwork(chip.Label);
        else
            NavigateToStudio(chip.Label);
    }

    private void BuildStudioNetworkChips()
    {
        _studioNetworkChips = [];
        if (_serie is null)
            return;

        if (!string.IsNullOrWhiteSpace(_serie.Network))
            _studioNetworkChips.Add(new SerieStudioNetworkChip(_serie.Network, IsNetwork: true));

        foreach (var studio in _serie.Studios ?? [])
        {
            if (_studioNetworkChips.Count(chip => !chip.IsNetwork) >= 1)
                break;

            if (_studioNetworkChips.Any(chip => string.Equals(chip.Label, studio, StringComparison.OrdinalIgnoreCase)))
                continue;

            _studioNetworkChips.Add(new SerieStudioNetworkChip(studio, IsNetwork: false));
        }
    }

    private Guid? GetLibraryIdForReIdentify()
    {
        if (_serie?.LibraryId is { } libraryId)
            return libraryId;

        return _serie?.IndexedFiles?.FirstOrDefault()?.LibraryId;
    }

    private async Task ResolveLibraryGroupIdAsync()
    {
        var libraryId = GetLibraryIdForReIdentify();
        var groups = await LibraryService.GetLibraryGroupsAsync();
        _libraryGroupId = LibraryGroupBrowseNavigationHelper.ResolveGroupId(
            groups,
            libraryId,
            LibraryMediaType.Serie);
    }

    private void NavigateToGenre(string genre)
    {
        if (!_libraryGroupId.HasValue)
            return;

        NavigationManager.NavigateTo(
            LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(_libraryGroupId.Value, genre: genre));
    }

    private async Task LoadSimilarMediaAsync()
    {
        if (_serie is null) return;

        try
        {
            var similar = await k7ServerService.GetSimilarMediaAsync(_serie.Id);
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

    private async Task MarkSerieWatchedAsync()
    {
        if (_serie is null)
            return;

        var success = await WatchStateActions.ApplyAsync(
            k7ServerService,
            CacheStore,
            DialogService,
            Snackbar,
            S,
            _serie.Id,
            watched: true,
            WatchStateScope.Serie);

        if (success)
            await ReloadSerieAsync();
    }

    private async Task MarkSerieUnwatchedAsync()
    {
        if (_serie is null)
            return;

        var success = await WatchStateActions.ApplyAsync(
            k7ServerService,
            CacheStore,
            DialogService,
            Snackbar,
            S,
            _serie.Id,
            watched: false,
            WatchStateScope.Serie);

        if (success)
            await ReloadSerieAsync();
    }

    private async Task ReloadSerieAsync()
    {
        var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
        if (media is not SerieDto serie)
            return;

        _serie = serie;
        _serieUserRating = GetUserRating(serie.Ratings);
        _seasons = (serie.Seasons ?? [])
            .OrderBy(s => s.SeasonNumber == 0 ? int.MaxValue : s.SeasonNumber)
            .ToList();
        BuildStudioNetworkChips();
        StateHasChanged();
    }

    private string FormatSeasonNumber(int seasonNumber) => string.Format(S["SeasonNumber"], seasonNumber);

    private async Task ExcludeSimilarForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            _similarMedia.RemoveAll(m => m.Id == item.Id || m.ParentId == item.Id);
    }

    private Task ExcludeSimilarForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);

    private async Task OpenReviewDialogAsync()
    {
        if (_serie is null)
            return;

        var changed = await MediaReviewDialogHelper.OpenAsync(DialogService, ReviewDialogL, _serie.Id, _serie.Title);
        if (!changed)
            return;

        var media = await k7ServerService.GetMediaAsync(_serie.Id);
        if (media is SerieDto serie)
        {
            _serie = serie;
            _serieUserRating = GetUserRating(serie.Ratings);
        }

        if (_reviewsSection is not null)
            await _reviewsSection.RefreshAsync();
    }

    public async ValueTask DisposeAsync()
    {
        K7HubClient.MediaIndexedFilesUpdated -= OnMediaIndexedFilesUpdated;
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
        _progressRefreshRunner?.Dispose();
        _indexedFilesDebounceTimer?.Dispose();
        _metadataRefreshWatcher?.Dispose();

        if (_tvScrollInitialized)
            await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.dispose", _tvScrollRoot);
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)Math.Round(value)
            : null;

    private readonly record struct SerieStudioNetworkChip(string Label, bool IsNetwork);
}
