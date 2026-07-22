using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Enums;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class SerieSeason : IAsyncDisposable
{
    [Parameter]
    public required string SerieId { get; set; }

    [Parameter]
    public int SeasonNumber { get; set; }

    private SerieSeasonDto? _season;
    private string? _backdropUrl;
    private string? _dominantColor;
    private string? _logoUrl;
    private List<LiteSerieEpisodeDto> _episodes = [];
    private int? _previousSeasonNumber;
    private int? _nextSeasonNumber;
    private List<int> _allSeasonNumbers = [];
    private string _pageTitle = "";
    private bool _loading = true;
    private bool _canRate;
    private int? _seasonUserRating;
    private string? _focusEpisodeFragment;
    private bool _isTv;
    private LiteSerieEpisodeDto? _focusedEpisode;
    private string? _focusedStillUrl;
    private string? _previousStillUrl;
    private Carousel? _tvCarousel;
    private ElementReference _seasonTvRoot;
    private bool _seasonTvScrollInitialized;
    private bool _isFederated;
    private readonly Dictionary<Guid, IReadOnlyList<LitePersonRoleDto>> _episodeCastCache = [];
    private IReadOnlyList<PersonRoleDisplayHelper.GroupedDisplay> _focusedEpisodeDisplayableCast = [];
    private Guid? _castLoadEpisodeId;

    private bool HasDisplayableCast => _focusedEpisodeDisplayableCast.Count > 0;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _focusEpisodeFragment = null;
        _focusedEpisodeDisplayableCast = [];
        _castLoadEpisodeId = null;
        _seasonTvScrollInitialized = false;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);

        var serieMedia = await k7ServerService.GetMediaAsync(Guid.Parse(SerieId));
        if (serieMedia is not SerieDto serie)
        {
            _loading = false;
            return;
        }

        var backdropPicture = serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
        _backdropUrl = apiClient.GetAbsoluteUri(
            backdropPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
        _dominantColor = backdropPicture?.DominantColor;

        _logoUrl = apiClient.GetAbsoluteUri(
            serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Logo)
                ?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

        var seasonSummary = serie.Seasons?
            .OrderBy(s => s.SeasonNumber)
            .ToList() ?? [];

        var currentIndex = seasonSummary.FindIndex(s => s.SeasonNumber == SeasonNumber);
        if (currentIndex < 0)
        {
            _loading = false;
            return;
        }

        _previousSeasonNumber = currentIndex > 0 ? seasonSummary[currentIndex - 1].SeasonNumber : null;
        _nextSeasonNumber = currentIndex < seasonSummary.Count - 1 ? seasonSummary[currentIndex + 1].SeasonNumber : null;
        _allSeasonNumbers = seasonSummary.Select(s => s.SeasonNumber).ToList();

        var seasonMedia = await k7ServerService.GetMediaAsync(seasonSummary[currentIndex].Id);
        if (seasonMedia is SerieSeasonDto seasonDto)
        {
            _season = seasonDto;
            _seasonUserRating = GetUserRating(seasonDto.Ratings);

            _episodes = (seasonDto.Episodes ?? [])
                .OrderBy(e => e.EpisodeNumber)
                .ToList();

            _isFederated = _episodes.Count > 0
                && _episodes.All(e => e.IndexedFileId is null && e.RemoteIndexedFileId is not null);

            _pageTitle = SeasonNumber == 0
                ? $"{serie.Title} - {L["Specials"]}"
                : $"{serie.Title} - {string.Format(L["SeasonNumber"], SeasonNumber)}";
        }

        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            _focusEpisodeFragment = uri.Fragment;
        }

        // Set initial focused episode for TV
        if (_isTv && _episodes.Count > 0)
        {
            var targetEpNumber = ParseEpisodeFragment(_focusEpisodeFragment);
            _focusedEpisode = (targetEpNumber is not null
                ? _episodes.FirstOrDefault(e => e.EpisodeNumber == targetEpNumber)
                : null) ?? _episodes[0];
            _focusedStillUrl = GetEpisodeStillUrl(_focusedEpisode, MetadataPictureSize.Medium);
            if (_episodeCastCache.TryGetValue(_focusedEpisode.Id, out var cached))
                ApplyFocusedEpisodeCast(cached);
            else
                await LoadFocusedEpisodeCastAsync(_focusedEpisode);
        }

        _loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isTv && _season is not null && !_loading)
        {
            try
            {
                if (!_seasonTvScrollInitialized)
                {
                    await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.init", _seasonTvRoot);
                    _seasonTvScrollInitialized = true;
                }
                else
                {
                    await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.sync", _seasonTvRoot);
                }
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
        }

        if (_focusEpisodeFragment is not null)
        {
            var elementId = _focusEpisodeFragment.TrimStart('#');
            _focusEpisodeFragment = null;

            try
            {
                if (_isTv && _tvCarousel is not null)
                {
                    var targetEpNumber = ParseEpisodeFragment("#" + elementId);
                    if (targetEpNumber is not null)
                    {
                        var index = _episodes.FindIndex(e => e.EpisodeNumber == targetEpNumber);
                        if (index >= 0)
                        {
                            await _tvCarousel.ScrollToIndexAsync(index);
                            await JSRuntime.InvokeVoidAsync("K7.focusById", elementId);
                        }
                    }
                }
                else
                {
                    await JSRuntime.InvokeVoidAsync("K7.scrollToElement", elementId);
                    await JSRuntime.InvokeVoidAsync("K7.focusById", elementId);
                }
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
        }
    }

    private static int? ParseEpisodeFragment(string? fragment)
    {
        if (fragment is null) return null;
        var raw = fragment.TrimStart('#');
        if (raw.StartsWith("ep-") && int.TryParse(raw[3..], out var num))
            return num;
        return null;
    }

    private string? GetEpisodeStillUrl(LiteSerieEpisodeDto episode, MetadataPictureSize size = MetadataPictureSize.Small)
    {
        if (episode.StillImageId is null) return null;
        return apiClient.GetAbsoluteUri(
            episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                ?.GetUri(size)?.OriginalString)?.AbsoluteUri;
    }

    private void OnTvEpisodeFocus(LiteSerieEpisodeDto episode)
    {
        if (_focusedEpisode?.Id == episode.Id)
            return;

        _focusedEpisode = episode;
        _previousStillUrl = _focusedStillUrl;
        _focusedStillUrl = GetEpisodeStillUrl(episode, MetadataPictureSize.Medium);

        if (_episodeCastCache.TryGetValue(episode.Id, out var cached))
            ApplyFocusedEpisodeCast(cached);
        else
            LoadFocusedEpisodeCastAsync(episode).FireAndForget();

        SyncEpisodeAnchorInUrl(episode.EpisodeNumber);
        StateHasChanged();
    }

    private void SyncEpisodeAnchorInUrl(int episodeNumber)
    {
        try
        {
            _ = JSRuntime.InvokeVoidAsync("K7.replaceUrlHash", $"ep-{episodeNumber}");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    private async Task LoadFocusedEpisodeCastAsync(LiteSerieEpisodeDto episode)
    {
        var loadId = episode.Id;
        _castLoadEpisodeId = loadId;

        var media = await k7ServerService.GetMediaAsync(episode.Id);
        if (_castLoadEpisodeId != loadId || _focusedEpisode?.Id != loadId)
            return;

        var roles = media is SerieEpisodeDto episodeDto
            ? episodeDto.PersonRoles ?? []
            : [];

        _episodeCastCache[loadId] = roles;
        ApplyFocusedEpisodeCast(roles);
        StateHasChanged();
    }

    private void ApplyFocusedEpisodeCast(IReadOnlyList<LitePersonRoleDto> roles)
    {
        _focusedEpisodeDisplayableCast = PersonRoleDisplayHelper.GroupForCarousel(roles);
    }

    private Task OpenSeasonOverviewDialogAsync()
    {
        if (_season is null || string.IsNullOrWhiteSpace(_season.Overview))
            return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _season.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private Task OpenSynopsisDialogAsync()
    {
        if (_focusedEpisode is null || string.IsNullOrWhiteSpace(_focusedEpisode.Overview)) return Task.CompletedTask;

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters
        {
            { "ContentText", _focusedEpisode.Overview },
            { "ButtonText", S["Cancel"].Value }
        };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private async Task OnTvEpisodeKeyDown(KeyboardEventArgs e, LiteSerieEpisodeDto episode)
    {
        if (e.Key is "Enter")
        {
            await PlayEpisodeAsync(episode);
        }
    }

    private async Task PlayEpisodeAsync(LiteSerieEpisodeDto episode)
    {
        var episodeMedia = await k7ServerService.GetMediaAsync(episode.Id);
        if (episodeMedia is not SerieEpisodeDto episodeDto) return;

        double? startPosition = null;
        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && episode.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            startPosition = episode.UserState.LastPlaybackPosition;
        }

        // Try local file first, then remote
        var indexedFile = episodeDto.IndexedFiles?.FirstOrDefault();
        if (indexedFile is not null)
        {
            var videoMetadata = indexedFile.FileMetadata as VideoFileMetadataDto;
            if (videoMetadata is null) return;

            PlaybackProgressTracker.StartTracking(episode.Id,
                await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
                Guid.Parse(SerieId),
                indexedFile.Id);

            var episodeTitle = episode.Title ?? $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";
            var coverUrl = GetEpisodeStillUrl(episode);

            await PlayerService.PlayIndexedFileAsync(
                indexedFile.Id,
                videoMetadata.AudioTracks ?? [],
                videoMetadata.SubtitleTracks,
                videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
                videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
                videoMetadata.VideoResolution,
                videoMetadata.Thumbnails?.Uri?.ToString(),
                episode.Id,
                episodeTitle,
                coverUrl,
                startPosition,
                videoMetadata.Chapters);
            return;
        }

        // Federated episode - use remote file
        var remoteFile = episodeDto.RemoteIndexedFiles?.FirstOrDefault();
        if (remoteFile is null) return;

        PlaybackProgressTracker.StartTracking(episode.Id,
            await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
            Guid.Parse(SerieId));

        var epTitle = episode.Title ?? $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";
        var cover = GetEpisodeStillUrl(episode);

        var details = await FederationService.GetRemoteFileDetailsAsync(remoteFile.Id);
        var remoteVideoMetadata = details?.FileMetadata as VideoFileMetadataDto;

        await PlayerService.PlayRemoteIndexedFileAsync(
            remoteFile.Id,
            remoteVideoMetadata?.AudioTracks ?? [],
            remoteVideoMetadata?.SubtitleTracks,
            remoteVideoMetadata?.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            remoteVideoMetadata?.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            remoteVideoMetadata?.VideoResolution,
            episode.Id,
            epTitle,
            cover,
            startPosition);
    }

    private void GoToPreviousSeason()
    {
        if (_previousSeasonNumber is not null)
            NavigationManager.NavigateTo($"/series/{SerieId}/seasons/{_previousSeasonNumber}");
    }

    private void GoToNextSeason()
    {
        if (_nextSeasonNumber is not null)
            NavigationManager.NavigateTo($"/series/{SerieId}/seasons/{_nextSeasonNumber}");
    }

    private void GoToSeason(int seasonNumber)
    {
        if (seasonNumber != SeasonNumber)
            NavigationManager.NavigateTo($"/series/{SerieId}/seasons/{seasonNumber}");
    }

    private void NavigateToSerie() => NavigationManager.NavigateTo($"/series/{SerieId}");

    private async Task DetectIntrosOutrosAsync()
    {
        if (_season is null)
            return;

        await k7ServerService.DetectMediaSegmentsAsync(_season.Id);
    }

    private IReadOnlyList<DownloadRequest> GetDownloadRequests()
    {
        return _episodes
            .Where(e => e.IndexedFileId.HasValue)
            .Select(e => new DownloadRequest
            {
                IndexedFileId = e.IndexedFileId!.Value,
                MediaId = e.Id,
                Title = e.Title ?? $"E{e.EpisodeNumber}",
                MediaType = MediaType.SerieEpisode,
                IsCacheItem = false
            })
            .ToList();
    }

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_season is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _season }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var media = await k7ServerService.GetMediaAsync(_season.Id);
            if (media is SerieSeasonDto season)
            {
                _season = season;
                StateHasChanged();
            }
        }
    }

    private async Task MarkSeasonWatchedAsync()
    {
        if (_season is null)
            return;

        var success = await WatchStateActions.ApplyAsync(
            k7ServerService,
            CacheStore,
            DialogService,
            Snackbar,
            S,
            _season.Id,
            watched: true,
            WatchStateScope.Season,
            _episodes.Count);

        if (success)
            await ReloadSeasonAsync();
    }

    private async Task MarkSeasonUnwatchedAsync()
    {
        if (_season is null)
            return;

        var success = await WatchStateActions.ApplyAsync(
            k7ServerService,
            CacheStore,
            DialogService,
            Snackbar,
            S,
            _season.Id,
            watched: false,
            WatchStateScope.Season,
            _episodes.Count);

        if (success)
            await ReloadSeasonAsync();
    }

    private async Task OnEpisodeWatchStateChangedAsync(LiteSerieEpisodeDto episode)
    {
        var media = await k7ServerService.GetMediaAsync(episode.Id);
        if (media is SerieEpisodeDto updated)
        {
            var index = _episodes.FindIndex(e => e.Id == episode.Id);
            if (index >= 0)
                _episodes[index] = _episodes[index] with { UserState = updated.UserState };
        }

        StateHasChanged();
    }

    private async Task ReloadSeasonAsync()
    {
        if (_season is null)
            return;

        var media = await k7ServerService.GetMediaAsync(_season.Id);
        if (media is SerieSeasonDto season)
        {
            _season = season;
            _seasonUserRating = GetUserRating(season.Ratings);
            _episodes = (season.Episodes ?? [])
                .OrderBy(e => e.EpisodeNumber)
                .ToList();
            StateHasChanged();
        }
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)value
            : null;

    public async ValueTask DisposeAsync()
    {
        if (!_seasonTvScrollInitialized)
            return;

        try
        {
            await JSRuntime.InvokeVoidAsync("K7.TvDetailScroll.dispose", _seasonTvRoot);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }
}
