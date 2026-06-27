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
    private bool _isAdmin;
    private bool _permissionsLoaded;
    private bool _isTv;
    private ElementReference _tvScrollRoot;
    private bool _tvScrollInitialized;
    private Guid? _libraryGroupId;
    private List<SerieStudioNetworkChip> _studioNetworkChips = [];

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
            _permissionsLoaded = true;
        }

        _loading = true;
        _tvScrollInitialized = false;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        var media = await k7ServerService.GetMediaAsync(Guid.Parse(Id));
        if (media is SerieDto serie)
        {
            _serie = serie;

            _posterUrl = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            _backdropUrl = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            _dominantColor = serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)?.DominantColor;

            _logoUrl = apiClient.GetAbsoluteUri(
                serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Logo)?
                    .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

            _seasons = (serie.Seasons ?? [])
                .OrderBy(s => s.SeasonNumber == 0 ? int.MaxValue : s.SeasonNumber)
                .ToList();

            BuildStudioNetworkChips();
            await ResolveLibraryGroupIdAsync();
        }
        else
        {
            _libraryGroupId = null;
            _studioNetworkChips = [];
        }

        _loading = false;

        _ = LoadSimilarMediaAsync();
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
        return apiClient.GetAbsoluteUri(
            season.Poster?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
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

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.MediaId, _serie.Id },
            { x => x.InitialSearchQuery, _serie.Title },
            { x => x.MediaType, K7.Server.Domain.Enums.MediaType.Serie }
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

    private Task OpenTrailerAsync()
    {
        if (_serie?.Trailers is not { Count: > 0 }) return Task.CompletedTask;

        var trailer = _serie.Trailers.FirstOrDefault(t => t.Type == "Trailer") ?? _serie.Trailers[0];
        var parameters = new K7DialogParameters<TrailerDialog>
        {
            { x => x.TrailerKey, trailer.Key },
            { x => x.TrailerSite, trailer.Site ?? "YouTube" }
        };
        var options = new K7DialogOptions { FullScreen = true, CloseOnEscapeKey = true, CloseButton = true };
        return DialogService.ShowAsync<TrailerDialog>(trailer.Name ?? L["Trailer"], parameters, options);
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

    private async Task ResolveLibraryGroupIdAsync()
    {
        var libraryId = _serie?.IndexedFiles?.FirstOrDefault()?.LibraryId;
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

    public async ValueTask DisposeAsync()
    {
        if (_tvScrollInitialized)
            await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.dispose", _tvScrollRoot);
    }

    private readonly record struct SerieStudioNetworkChip(string Label, bool IsNetwork);
}
