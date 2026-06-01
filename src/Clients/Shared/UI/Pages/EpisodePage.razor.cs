using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class EpisodePage
{
    [Parameter] public string SerieId { get; set; } = "";
    [Parameter] public int SeasonNumber { get; set; }
    [Parameter] public int EpisodeNumber { get; set; }

    private SerieEpisodeDto? _episode;
    private IndexedFileDto? _indexedFile;
    private string? _stillUrl;
    private string? _dominantColor;
    private string _pageTitle = "";
    private bool _loading = true;
    private LiteSerieEpisodeDto? _previousEpisode;
    private LiteSerieEpisodeDto? _nextEpisode;

    private string DominantColorStyle => _dominantColor is not null
        ? $"--episode-dominant-color: {_dominantColor};"
        : "";

    protected override async Task OnParametersSetAsync()
    {
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
        _pageTitle = $"{_episode.SerieTitle} - S{SeasonNumber:00}E{EpisodeNumber:00} - {_episode.Title}";

        var still = _episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                    ?? _episode.Pictures?.FirstOrDefault();
        _stillUrl = still is not null
            ? apiClient.GetAbsoluteUri(still.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri
            : null;
        _dominantColor = still?.DominantColor;

        var episodeIndex = episodes.FindIndex(e => e.EpisodeNumber == EpisodeNumber);
        _previousEpisode = episodeIndex > 0 ? episodes[episodeIndex - 1] : null;
        _nextEpisode = episodeIndex < episodes.Count - 1 ? episodes[episodeIndex + 1] : null;

        _loading = false;
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
            var media = await k7ServerService.GetMediaAsync(_episode.Id);
            _episode = media as SerieEpisodeDto;
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

    private static string FormatDuration(int totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h{ts.Minutes:00}"
            : $"{ts.Minutes}min";
    }
}
