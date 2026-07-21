using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Enums;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
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
    private string? _posterUrl;
    private string? _backdropUrl;
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
    private MediaMetadataRefreshWatcher? _metadataRefreshWatcher;

    protected override void OnInitialized()
    {
        _metadataRefreshWatcher = new MediaMetadataRefreshWatcher(K7HubClient, InvokeAsync);
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
            _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        }

        var media = await k7ServerService.GetMediaAsync(
            Guid.Parse(Id),
            bypassCache: isBackgroundRefresh || isPicturesRefresh);
        if (media is SerieDto serie)
        {
            _serie = serie;
            _serieUserRating = GetUserRating(serie.Ratings);

            var cacheVersion = isPicturesRefresh
                ? DateTimeOffset.UtcNow
                : serie.LastMetadataRefreshedAt;

            var posterUri = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _posterUrl = MediaPictureUrlHelper.WithCacheBuster(posterUri, cacheVersion);

            var backdropUri = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _backdropUrl = MediaPictureUrlHelper.WithCacheBuster(backdropUri, cacheVersion);

            _dominantColor = serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)?.DominantColor;

            var logoUri = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Logo)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            _logoUrl = MediaPictureUrlHelper.WithCacheBuster(logoUri, cacheVersion);

            _seasons = (serie.Seasons ?? [])
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

        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isTv && !_loading && _serie is not null && HasTvScrollContent)
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

    private async Task WatchAsync()
    {
        if (_seasons.Count == 0)
            return;

        var episode = await SeriePlaybackHelper.ResolveEpisodeToPlayAsync(k7ServerService, _seasons);
        if (episode is null)
            return;

        await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);

        await SeriePlaybackHelper.PlayEpisodeAsync(
            episode,
            Guid.Parse(Id),
            k7ServerService,
            PlayerService,
            PlaybackProgressTracker,
            FeatureAccess,
            FederationService,
            apiClient);
    }

    private async Task OpenMediaReIdentifyDialogAsync()
    {
        if (_serie is null) return;

        var (searchQuery, searchYear) = ReIdentifySearchDefaultsHelper.FromIndexedFiles(
            _serie.IndexedFiles,
            MediaType.Serie,
            fallbackQuery: _serie.Title,
            fallbackYear: _serie.ReleaseDate?.Year);

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _serie.Id },
            { x => x.InitialSearchQuery, searchQuery },
            { x => x.InitialSearchYear, searchYear },
            { x => x.MediaType, MediaType.Serie },
            { x => x.LibraryId, GetLibraryIdForReIdentify() }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(L["ReIdentifyMediaDialogTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["ReIdentifyMediaSent"], K7Severity.Success);
            NavigationManager.NavigateTo("/");
        }
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

        await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);

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
        await ThemeSongPlaybackHelper.StopAsync(AmbientThemeService);
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
