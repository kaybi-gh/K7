using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class SerieSeason
{
    [Parameter]
    public required string SerieId { get; set; }

    [Parameter]
    public int SeasonNumber { get; set; }

    private SerieSeasonDto? _season;
    private string? _backdropUrl;
    private string? _logoUrl;
    private List<LiteSerieEpisodeDto> _episodes = [];
    private int? _previousSeasonNumber;
    private int? _nextSeasonNumber;
    private Guid? _expandedEpisodeId;
    private string _pageTitle = "";
    private bool _loading = true;
    private string? _focusEpisodeFragment;
    private bool _isTv;
    private LiteSerieEpisodeDto? _focusedEpisode;
    private string? _focusedStillUrl;
    private string? _previousStillUrl;
    private Carousel? _tvCarousel;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _expandedEpisodeId = null;
        _focusEpisodeFragment = null;
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        var serieMedia = await k7ServerService.GetMediaAsync(Guid.Parse(SerieId));
        if (serieMedia is not SerieDto serie)
        {
            _loading = false;
            return;
        }

        _backdropUrl = apiClient.GetAbsoluteUri(
            serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)
                ?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

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

        var seasonMedia = await k7ServerService.GetMediaAsync(seasonSummary[currentIndex].Id);
        if (seasonMedia is SerieSeasonDto seasonDto)
        {
            _season = seasonDto;

            _episodes = (seasonDto.Episodes ?? [])
                .OrderBy(e => e.EpisodeNumber)
                .ToList();

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
        }

        _loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusEpisodeFragment is not null)
        {
            var elementId = _focusEpisodeFragment.TrimStart('#');
            _focusEpisodeFragment = null;

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
        _focusedEpisode = episode;
        _previousStillUrl = _focusedStillUrl;
        _focusedStillUrl = GetEpisodeStillUrl(episode, MetadataPictureSize.Medium);
        var baseUri = NavigationManager.Uri.Split('#')[0];
        NavigationManager.NavigateTo($"{baseUri}#ep-{episode.EpisodeNumber}", replace: true);
        StateHasChanged();
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

    private void ToggleExpand(Guid episodeId)
    {
        _expandedEpisodeId = _expandedEpisodeId == episodeId ? null : episodeId;
    }

    private async Task PlayEpisodeAsync(LiteSerieEpisodeDto episode)
    {
        var episodeMedia = await k7ServerService.GetMediaAsync(episode.Id);
        if (episodeMedia is not SerieEpisodeDto episodeDto) return;

        var indexedFile = episodeDto.IndexedFiles?.FirstOrDefault();
        if (indexedFile is null) return;

        var videoMetadata = indexedFile.FileMetadata as VideoFileMetadataDto;
        if (videoMetadata is null) return;

        PlaybackProgressTracker.StartTracking(episode.Id,
            await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
            Guid.Parse(SerieId));

        await PlayerService.PlayIndexedFileAsync(
            indexedFile.Id,
            videoMetadata.AudioTracks ?? [],
            videoMetadata.SubtitleTracks,
            videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.VideoResolution,
            videoMetadata.Thumbnails?.Uri?.ToString(),
            episode.Id);

        if (await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && episode.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            PlayerService.Seek(episode.UserState.LastPlaybackPosition);
        }
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
}
