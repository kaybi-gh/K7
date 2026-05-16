using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class NextEpisodeOverlay : IDisposable
{
    [Parameter] public LiteSerieEpisodeDto? DemoEpisode { get; set; }
    [Parameter] public string? DemoStillUrl { get; set; }
    [Parameter] public int DemoCountdown { get; set; }
    [Parameter] public string? DemoCurrentStillUrl { get; set; }
    [Parameter] public LiteSerieEpisodeDto? DemoCurrentEpisode { get; set; }

    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private PlaybackProgressTracker PlaybackProgressTracker { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    private const int AutoPlayCountdown = 15;

    private LiteSerieEpisodeDto? _nextEpisode;
    private string? _stillUrl;
    private LiteSerieEpisodeDto? _currentEpisode;
    private string? _currentStillUrl;
    private bool _visible;
    private int _countdownSeconds;
    private Timer? _countdownTimer;
    private bool _disposed;
    private string _behavior = "AutoPlay";

    protected override void OnInitialized()
    {
        _behavior = DeviceStorage.Get(PreferenceKeys.NEXT_EPISODE_BEHAVIOR, "AutoPlay") ?? "AutoPlay";
        PlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    private async void OnPlaybackStateChanged(PlaybackState state)
    {
        if (_disposed) return;

        if (state == PlaybackState.Ended)
        {
            await HandlePlaybackEndedAsync();
        }
        else if (state == PlaybackState.Playing && _visible)
        {
            Reset();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandlePlaybackEndedAsync()
    {
        if (_behavior == "Off") return;

        var serieId = PlaybackProgressTracker.CurrentSerieId;
        var episodeId = PlaybackProgressTracker.CurrentMediaId;
        if (serieId is null || episodeId is null) return;

        // Fetch current episode info for the left card
        try
        {
            var currentMedia = await MediaService.GetMediaAsync(episodeId.Value);
            if (currentMedia is SerieEpisodeDto currentDto)
            {
                _currentEpisode = new LiteSerieEpisodeDto
                {
                    Id = currentDto.Id,
                    Title = currentDto.Title,
                    EpisodeNumber = currentDto.EpisodeNumber,
                    SeasonNumber = currentDto.SeasonNumber,
                    Pictures = currentDto.Pictures,
                };
                _currentStillUrl = ApiClient.GetAbsoluteUri(
                    currentDto.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?
                        .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
            }
        }
        catch { }

        try
        {
            _nextEpisode = await MediaService.GetNextEpisodeAsync(serieId.Value, episodeId.Value);
        }
        catch
        {
            return;
        }

        if (_nextEpisode is null) return;

        _stillUrl = ApiClient.GetAbsoluteUri(
            _nextEpisode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?
                .GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

        _visible = true;

        if (_behavior == "AutoPlay")
        {
            _countdownSeconds = AutoPlayCountdown;
            _countdownTimer = new Timer(OnCountdownTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        await InvokeAsync(StateHasChanged);
    }

    private async void OnCountdownTick(object? state)
    {
        if (_disposed) return;

        _countdownSeconds--;

        if (_countdownSeconds <= 0)
        {
            _countdownTimer?.Dispose();
            _countdownTimer = null;
            await InvokeAsync(async () => await PlayNextAsync());
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task ReplayAsync()
    {
        Reset();
        PlayerService.Seek(0);
        PlayerService.Play();
        return Task.CompletedTask;
    }

    private async Task PlayNextAsync()
    {
        if (_nextEpisode is null) return;

        var nextEpisodeId = _nextEpisode.Id;
        var serieId = PlaybackProgressTracker.CurrentSerieId;

        Reset();

        PlaybackProgressTracker.StopTracking();

        var episodeMedia = await MediaService.GetMediaAsync(nextEpisodeId);
        if (episodeMedia is not SerieEpisodeDto episodeDto) return;

        var indexedFile = episodeDto.IndexedFiles?.FirstOrDefault();
        if (indexedFile is null) return;

        var videoMetadata = indexedFile.FileMetadata as VideoFileMetadataDto;
        if (videoMetadata is null) return;

        PlaybackProgressTracker.StartTracking(nextEpisodeId,
            await FeatureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
            serieId);

        await PlayerService.PlayIndexedFileAsync(
            indexedFile.Id,
            videoMetadata.AudioTracks ?? [],
            videoMetadata.SubtitleTracks,
            videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.VideoResolution,
            videoMetadata.Thumbnails?.Uri?.ToString(),
            nextEpisodeId);

        StateHasChanged();
    }

    private async Task Dismiss()
    {
        Reset();
        PlayerService.Stop();
        await PlayerService.HideAsync();
    }

    private void Reset()
    {
        _visible = false;
        _nextEpisode = null;
        _stillUrl = null;
        _currentEpisode = null;
        _currentStillUrl = null;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        _countdownSeconds = 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        PlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _countdownTimer?.Dispose();
    }
}
