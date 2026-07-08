using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SerieEpisode : IAsyncDisposable
{
    [Parameter] public string SerieId { get; set; } = "";
    [Parameter] public int SeasonNumber { get; set; }
    [Parameter] public int EpisodeNumber { get; set; }

    private SerieEpisodeDto? _episode;
    private IndexedFileDto? _indexedFile;
    private string? _stillUrl;
    private bool _useHdStillBackdrop;
    private string? _dominantColor;
    private string _pageTitle = "";
    private bool _loading = true;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _canRate;
    private int? _episodeUserRating;
    private bool _isAdmin;
    private LiteSerieEpisodeDto? _previousEpisode;
    private LiteSerieEpisodeDto? _nextEpisode;
    private List<LiteSerieEpisodeDto> _moreEpisodes = [];
    private MediaReviewsSection? _reviewsSection;
    private ElementReference _scrollRoot;
    private MediaMetadataRefreshWatcher? _metadataRefreshWatcher;
    private Guid? _watchedEpisodeId;

    private string DominantColorStyle => DominantColorCss.ToVariableStyle("--media-dominant-color", _dominantColor);

    protected override void OnInitialized()
    {
        _metadataRefreshWatcher = new MediaMetadataRefreshWatcher(K7HubClient, InvokeAsync);
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadEpisodeAsync();
    }

    private async Task LoadEpisodeAsync(bool isBackgroundRefresh = false, bool isPicturesRefresh = false)
    {
        if (isBackgroundRefresh || isPicturesRefresh)
        {
            if (_episode is null)
                return;

            var refreshedMedia = await k7ServerService.GetMediaAsync(_episode.Id, bypassCache: true);
            _episode = refreshedMedia as SerieEpisodeDto;
            if (_episode is null)
                return;

            _indexedFile = _episode.IndexedFiles?.FirstOrDefault();
            UpdateStillFromEpisode(isPicturesRefresh ? DateTimeOffset.UtcNow : _episode.LastMetadataRefreshedAt);

            if (isBackgroundRefresh)
                Snackbar.Add(S["RefreshMetadataCompleted"], K7Severity.Success);

            StateHasChanged();
            return;
        }

        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);
        _canRate = await FeatureAccess.HasCapabilityAsync(Capability.CanRate);

        _loading = true;
        StateHasChanged();

        var serieMedia = await k7ServerService.GetMediaAsync(Guid.Parse(SerieId));
        if (serieMedia is not SerieDto serie)
        {
            _loading = false;
            return;
        }

        var seasonSummary = serie.Seasons?.FirstOrDefault(s => s.SeasonNumber == SeasonNumber);
        if (seasonSummary is null)
        {
            _loading = false;
            return;
        }

        var seasonMedia = await k7ServerService.GetMediaAsync(seasonSummary.Id);
        if (seasonMedia is not SerieSeasonDto seasonDto)
        {
            _loading = false;
            return;
        }

        var episodes = (seasonDto.Episodes ?? [])
            .OrderBy(e => e.EpisodeNumber)
            .ToList();

        var liteEpisode = episodes.FirstOrDefault(e => e.EpisodeNumber == EpisodeNumber);
        if (liteEpisode is null)
        {
            _loading = false;
            return;
        }

        var media = await k7ServerService.GetMediaAsync(liteEpisode.Id);
        _episode = media as SerieEpisodeDto;
        if (_episode is null)
        {
            _loading = false;
            return;
        }

        _indexedFile = _episode.IndexedFiles?.FirstOrDefault();
        _episodeUserRating = GetUserRating(_episode.Ratings);
        _pageTitle = $"{_episode.SerieTitle} - S{SeasonNumber:00}E{EpisodeNumber:00} - {_episode.Title}";

        UpdateStillFromEpisode(_episode.LastMetadataRefreshedAt);
        SetupMetadataRefreshWatcher(_episode.Id);

        var episodeIndex = episodes.FindIndex(e => e.EpisodeNumber == EpisodeNumber);
        _previousEpisode = episodeIndex > 0 ? episodes[episodeIndex - 1] : null;
        _nextEpisode = episodeIndex < episodes.Count - 1 ? episodes[episodeIndex + 1] : null;
        _moreEpisodes = episodes.Where(e => e.EpisodeNumber != EpisodeNumber).ToList();

        _loading = false;
    }

    private void SetupMetadataRefreshWatcher(Guid episodeId)
    {
        if (_watchedEpisodeId == episodeId)
            return;

        _watchedEpisodeId = episodeId;
        _metadataRefreshWatcher?.Watch(
            episodeId,
            () => LoadEpisodeAsync(isBackgroundRefresh: true),
            () => LoadEpisodeAsync(isPicturesRefresh: true));
    }

    private void UpdateStillFromEpisode(DateTimeOffset? cacheVersion = null)
    {
        if (_episode is null)
            return;

        var still = _episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                    ?? _episode.Pictures?.FirstOrDefault();
        var stillUri = apiClient.GetAbsoluteUri(
            still?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
        _stillUrl = MediaPictureUrlHelper.WithCacheBuster(stillUri, cacheVersion);
        _useHdStillBackdrop = MetadataPictureDisplayHelper.IsHdStill(still);
        _dominantColor = still?.DominantColor;
    }

    private async Task PlayAsync()
    {
        if (_episode is null || _indexedFile is null) return;

        var videoMetadata = _indexedFile.FileMetadata as VideoFileMetadataDto;
        if (videoMetadata is null) return;

        PlaybackProgressTracker.StartTracking(_episode.Id,
            await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
            Guid.Parse(SerieId),
            _indexedFile.Id);

        var episodeTitle = _episode.Title ?? $"S{SeasonNumber:D2}E{EpisodeNumber:D2}";

        await PlayerService.PlayIndexedFileAsync(
            _indexedFile.Id,
            videoMetadata.AudioTracks ?? [],
            videoMetadata.SubtitleTracks,
            videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.VideoResolution,
            videoMetadata.Thumbnails?.Uri?.ToString(),
            _episode.Id,
            episodeTitle,
            _stillUrl);

        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && _episode.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            PlayerService.Seek(_episode.UserState.LastPlaybackPosition);
        }
    }

    private async Task OpenEditMetadataAsync()
    {
        if (_episode is null) return;
        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Media, _episode }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(S["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            var media = await k7ServerService.GetMediaAsync(_episode.Id, bypassCache: true);
            _episode = media as SerieEpisodeDto;
            UpdateStillFromEpisode(DateTimeOffset.UtcNow);
            StateHasChanged();
        }
    }

    private async Task OpenIndexedFilesAsync()
    {
        if (_episode is null) return;
        var parameters = new K7DialogParameters<IndexedFilesDialog>
        {
            { x => x.Media, _episode }
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        await DialogService.ShowAsync<IndexedFilesDialog>(S["IndexedVersions"], parameters, options);
    }

    private void NavigateToSerie() => NavigationManager.NavigateTo($"/series/{SerieId}");

    private void NavigateToSeason() => NavigationManager.NavigateTo($"/series/{SerieId}/seasons/{SeasonNumber}");

    private Task OpenOverviewDialogAsync()
    {
        if (_episode is null || string.IsNullOrWhiteSpace(_episode.Overview))
            return Task.CompletedTask;

        var parameters = new K7DialogParameters<OverviewDialog>
        {
            { "ContentText", _episode.Overview },
        };
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        return DialogService.ShowAsync<OverviewDialog>(L["Overview"], parameters, options);
    }

    private static string FormatDuration(int totalMinutes)
    {
        if (totalMinutes >= 60)
        {
            var hours = totalMinutes / 60;
            var mins = totalMinutes % 60;
            return mins > 0 ? $"{hours}h{mins:00}" : $"{hours}h";
        }

        return $"{totalMinutes}min";
    }

    private int GetDisplayRuntime()
    {
        if (_episode?.Runtime is > 0)
            return _episode.Runtime.Value;

        if (_indexedFile?.FileMetadata is VideoFileMetadataDto video && video.Duration.TotalMinutes > 0)
            return (int)Math.Round(video.Duration.TotalMinutes);

        return 0;
    }

    private string? GetEpisodeStillUrl(LiteSerieEpisodeDto episode)
    {
        if (episode.StillImageId is null) return null;
        return apiClient.GetAbsoluteUri(
            episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }

    private async Task ReloadEpisodeAsync()
    {
        if (_episode is null)
            return;

        var media = await k7ServerService.GetMediaAsync(_episode.Id, bypassCache: true);
        _episode = media as SerieEpisodeDto;
        _indexedFile = _episode?.IndexedFiles?.FirstOrDefault();
        UpdateStillFromEpisode(DateTimeOffset.UtcNow);
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _metadataRefreshWatcher?.Dispose();
        await ValueTask.CompletedTask;
    }

    private async Task ToggleWatchStateAsync()
    {
        if (_episode is null)
            return;

        var watched = _episode.UserState?.IsCompleted != true;
        var success = await WatchStateActions.ApplyAsync(
            k7ServerService,
            CacheStore,
            DialogService,
            Snackbar,
            S,
            _episode.Id,
            watched,
            WatchStateScope.Item);

        if (success)
            await ReloadEpisodeAsync();
    }

    private async Task ExcludeForSelfAsync()
    {
        if (_episode is null)
            return;

        var model = new MediaCardViewModel
        {
            Id = _episode.Id.ToString(),
            Title = _episode.Title ?? $"E{_episode.EpisodeNumber}"
        };

        await MediaCardExcludeActions.ExcludeForSelfAsync(model, UserAdminService, Snackbar, S);
    }

    private async Task ExcludeForOthersAsync()
    {
        if (_episode is null)
            return;

        var model = new MediaCardViewModel
        {
            Id = _episode.Id.ToString(),
            Title = _episode.Title ?? $"E{_episode.EpisodeNumber}"
        };

        await MediaCardExcludeActions.ExcludeForOthersAsync(model, DialogService, Snackbar, S);
    }

    private async Task ExcludeCarouselEpisodeForSelfAsync(LiteSerieEpisodeDto episode)
    {
        var model = new MediaCardViewModel
        {
            Id = episode.Id.ToString(),
            Title = episode.Title ?? $"E{episode.EpisodeNumber}"
        };

        var excluded = await MediaCardExcludeActions.ExcludeForSelfAsync(model, UserAdminService, Snackbar, S);
        if (excluded)
        {
            _moreEpisodes.RemoveAll(e => e.Id == episode.Id);
            StateHasChanged();
        }
    }

    private Task ExcludeCarouselEpisodeForOthersAsync(LiteSerieEpisodeDto episode)
    {
        var model = new MediaCardViewModel
        {
            Id = episode.Id.ToString(),
            Title = episode.Title ?? $"E{episode.EpisodeNumber}"
        };

        return MediaCardExcludeActions.ExcludeForOthersAsync(model, DialogService, Snackbar, S);
    }

    private async Task ReloadMoreEpisodesAsync()
    {
        if (_episode is null)
            return;

        var seasonMedia = await k7ServerService.GetMediaAsync(_episode.SeasonId);
        if (seasonMedia is not SerieSeasonDto seasonDto)
            return;

        _moreEpisodes = (seasonDto.Episodes ?? [])
            .OrderBy(e => e.EpisodeNumber)
            .Where(e => e.EpisodeNumber != EpisodeNumber)
            .ToList();
        StateHasChanged();
    }

    private async Task OpenReviewDialogAsync()
    {
        if (_episode is null)
            return;

        var changed = await MediaReviewDialogHelper.OpenAsync(
            DialogService,
            ReviewDialogL,
            _episode.Id,
            _episode.Title ?? $"E{_episode.EpisodeNumber}");

        if (!changed)
            return;

        var media = await k7ServerService.GetMediaAsync(_episode.Id);
        _episode = media as SerieEpisodeDto;
        _episodeUserRating = GetUserRating(_episode?.Ratings);
        if (_reviewsSection is not null)
            await _reviewsSection.RefreshAsync();
    }

    private static int? GetUserRating(IReadOnlyList<RatingDto>? ratings) =>
        ratings?.FirstOrDefault(r => r.Source == RatingSource.LocalUser)?.Value is double value
            ? (int)value
            : null;
}
