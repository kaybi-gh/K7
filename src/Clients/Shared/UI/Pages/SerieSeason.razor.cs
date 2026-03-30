using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SerieSeason
{
    [Parameter]
    public required string SerieId { get; set; }

    [Parameter]
    public int SeasonNumber { get; set; }

    private SerieSeasonDto? _season;
    private string? _backdropUrl;
    private string? _seasonPosterUrl;
    private List<LiteSerieEpisodeDto> _episodes = [];
    private int? _previousSeasonNumber;
    private int? _nextSeasonNumber;
    private Guid? _expandedEpisodeId;
    private string _pageTitle = "";
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _expandedEpisodeId = null;

        var serieMedia = await k7ServerService.GetMediaAsync(Guid.Parse(SerieId));
        if (serieMedia is not SerieDto serie)
        {
            _loading = false;
            return;
        }

        _backdropUrl = apiClient.GetAbsoluteUri(
            serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)?
                .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

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
            _seasonPosterUrl = apiClient.GetAbsoluteUri(
                seasonDto.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                    .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

            _episodes = (seasonDto.Episodes ?? [])
                .OrderBy(e => e.EpisodeNumber)
                .ToList();

            _pageTitle = SeasonNumber == 0
                ? $"{serie.Title} - {L["Specials"]}"
                : $"{serie.Title} - {string.Format(L["SeasonNumber"], SeasonNumber)}";
        }

        _loading = false;
    }

    private string? GetEpisodeStillUrl(LiteSerieEpisodeDto episode)
    {
        if (episode.StillImageId is null) return null;
        return apiClient.GetAbsoluteUri(
            episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
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
            videoMetadata.Thumbnails?.Uri?.ToString());

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
