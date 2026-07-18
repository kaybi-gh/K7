using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

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
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    private const int AutoPlayCountdownSeconds = 15;

    private LiteSerieEpisodeDto? _nextEpisode;
    private string? _stillUrl;
    private LiteSerieEpisodeDto? _currentEpisode;
    private string? _currentStillUrl;
    private bool _visible;
    private int _countdownSeconds;
    private int _countdownDurationSeconds;
    private bool _countdownActive;
    private bool _countdownPaused;
    private Timer? _countdownTimer;
    private bool _disposed;
    private bool _layerActive;
    private ElementReference _overlayRef;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;
    private string _behavior = "AutoPlay";

    private bool ShowAutoplayProgress => DemoCountdown > 0 || (_countdownActive && _behavior == "AutoPlay");

    private int CountdownDurationSeconds => DemoCountdown > 0 ? DemoCountdown : _countdownDurationSeconds;

    private int DisplayCountdownSeconds => DemoCountdown > 0 ? DemoCountdown : _countdownSeconds;

    private double AutoplayProgressPercent => CountdownDurationSeconds > 0
        ? (double)DisplayCountdownSeconds / CountdownDurationSeconds * 100
        : 0;

    protected override void OnInitialized()
    {
        _behavior = DeviceStorage.Get(PreferenceKeys.NEXT_EPISODE_BEHAVIOR, "AutoPlay") ?? "AutoPlay";
        PlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var overlayRendered = DemoEpisode is not null || DemoCurrentEpisode is not null || _visible;
        if (overlayRendered)
        {
            _closeCallbackRef ??= DotNetObjectReference.Create(new LayerCloseCallback(() => _ = InvokeAsync(Dismiss)));
            try
            {
                await SpatialNav.PushLayerAsync(_overlayRef, "overlay", new SpatialNavLayerOptions
                {
                    OnClose = _closeCallbackRef,
                    FocusSelector = ".k7-btn"
                });
                if (!_layerActive)
                {
                    _layerActive = true;
                    await SpatialNav.RefreshAsync();
                }
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException)
            {
            }
        }
        else if (_layerActive)
        {
            await PopLayerAsync();
        }
    }

    private void OnPlaybackStateChanged(PlaybackState state) => OnPlaybackStateChangedAsync(state).FireAndForget();

    private async Task OnPlaybackStateChangedAsync(PlaybackState state)
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
            StartAutoplayCountdown(AutoPlayCountdownSeconds);
        }

        await InvokeAsync(StateHasChanged);
    }

    private void StartAutoplayCountdown(int seconds)
    {
        _countdownDurationSeconds = seconds;
        _countdownSeconds = seconds;
        _countdownActive = true;
        _countdownPaused = false;
        _countdownTimer?.Dispose();
        _countdownTimer = new Timer(OnCountdownTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void OnOverlayActivity()
    {
        PauseCountdown();
    }

    private void PauseCountdown()
    {
        if (_countdownPaused || !_countdownActive)
            return;

        _countdownPaused = true;
        _countdownTimer?.Dispose();
        _countdownTimer = null;
        InvokeAsync(StateHasChanged);
    }

    private void OnCountdownTick(object? state) => OnCountdownTickAsync().FireAndForget();

    private async Task OnCountdownTickAsync()
    {
        if (_disposed || _countdownPaused)
            return;

        _countdownSeconds--;

        if (_countdownSeconds <= 0)
        {
            _countdownTimer?.Dispose();
            _countdownTimer = null;
            _countdownActive = false;
            await InvokeAsync(async () => await PlayNextAsync());
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task ReplayAsync()
    {
        PauseCountdown();
        Reset();
        PlayerService.Seek(0);
        PlayerService.Play();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task PlayNextAsync()
    {
        if (_nextEpisode is null) return;

        PauseCountdown();

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
            serieId,
            indexedFile.Id);

        await PlayerService.PlayIndexedFileAsync(
            indexedFile.Id,
            videoMetadata.AudioTracks ?? [],
            videoMetadata.SubtitleTracks,
            videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            videoMetadata.VideoResolution,
            videoMetadata.Thumbnails?.Uri?.ToString(),
            nextEpisodeId,
            chapters: videoMetadata.Chapters);

        StateHasChanged();
    }

    private async Task Dismiss()
    {
        PauseCountdown();
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
        _countdownDurationSeconds = 0;
        _countdownActive = false;
        _countdownPaused = false;
    }

    private async Task PopLayerAsync()
    {
        if (!_layerActive)
            return;

        _layerActive = false;
        try
        {
            await SpatialNav.PopLayerAsync(_overlayRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }

        _closeCallbackRef?.Dispose();
        _closeCallbackRef = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        PlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _countdownTimer?.Dispose();
        _closeCallbackRef?.Dispose();
        if (_layerActive)
        {
            _ = PopLayerAsync();
        }
    }
}
