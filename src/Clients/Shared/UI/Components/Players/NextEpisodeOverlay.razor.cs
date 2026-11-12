using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
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
    [Parameter] public bool DemoStillWatching { get; set; }

    [Inject] private IPlayerService PlayerService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private PlaybackProgressTracker PlaybackProgressTracker { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IUserPreferencesService UserPreferencesService { get; set; } = default!;
    [Inject] private InactivityPromptTracker InactivityTracker { get; set; } = default!;

    private const int AutoPlayCountdownSeconds = 15;
    private const int StillWatchingTimeoutSeconds = 120;

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
    private bool _stillWatchingPrompt;
    private Timer? _stillWatchingTimer;

    private bool ShowStillWatchingPrompt => DemoStillWatching || _stillWatchingPrompt;

    private bool ShowAutoplayProgress =>
        !ShowStillWatchingPrompt && (DemoCountdown > 0 || _countdownActive);

    private int CountdownDurationSeconds => DemoCountdown > 0 ? DemoCountdown : _countdownDurationSeconds;

    private int DisplayCountdownSeconds => DemoCountdown > 0 ? DemoCountdown : _countdownSeconds;

    private double AutoplayProgressPercent => CountdownDurationSeconds > 0
        ? (double)DisplayCountdownSeconds / CountdownDurationSeconds * 100
        : 0;

    protected override void OnInitialized()
    {
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
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
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
        var settings = await GetVideoPlayerSettingsAsync();
        if (settings.NextEpisodeBehavior == NextEpisodeBehavior.ClosePlayer)
        {
            await ClosePlayerAfterEndAsync();
            return;
        }

        var serieId = PlaybackProgressTracker.CurrentSerieId;
        var episodeId = PlaybackProgressTracker.CurrentMediaId;
        if (serieId is null || episodeId is null)
        {
            await ClosePlayerAfterEndAsync();
            return;
        }

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
                        .GetUri(MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Hero))?.OriginalString)?.AbsoluteUri;
            }
        }
        catch { }

        try
        {
            _nextEpisode = await MediaService.GetNextEpisodeAsync(serieId.Value, episodeId.Value);
        }
        catch
        {
            await ClosePlayerAfterEndAsync();
            return;
        }

        if (_nextEpisode is null)
        {
            await ClosePlayerAfterEndAsync();
            return;
        }

        _stillUrl = ApiClient.GetAbsoluteUri(
            _nextEpisode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)?
                .GetUri(MetadataPictureDisplayHelper.SizeFor(ImageDisplayRole.Hero))?.OriginalString)?.AbsoluteUri;

        var autoPlay = settings.NextEpisodeBehavior == NextEpisodeBehavior.PlayAfterCountdown;
        _stillWatchingPrompt = autoPlay
            && InactivityTracker.ShouldPrompt(settings.InactivityPromptAfterEpisodes, episodeId.Value);
        _visible = true;

        if (_stillWatchingPrompt)
        {
            StartStillWatchingTimeout();
        }
        else if (autoPlay)
        {
            StartAutoplayCountdown(AutoPlayCountdownSeconds);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task<VideoPlayerSettingsDto> GetVideoPlayerSettingsAsync()
    {
        if (PlayerService.VideoPlayerUxSettings is { } applied)
            return applied;

        try
        {
            return await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
        }
        catch
        {
            return new VideoPlayerSettingsDto();
        }
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

    /// <summary>
    /// Nobody answered the "still watching?" prompt: close the player so the device can sleep
    /// instead of holding the ended episode on screen all night.
    /// </summary>
    private void StartStillWatchingTimeout()
    {
        _stillWatchingTimer?.Dispose();
        _stillWatchingTimer = new Timer(
            OnStillWatchingTimeout,
            null,
            TimeSpan.FromSeconds(StillWatchingTimeoutSeconds),
            Timeout.InfiniteTimeSpan);
    }

    private void StopStillWatchingTimeout()
    {
        _stillWatchingTimer?.Dispose();
        _stillWatchingTimer = null;
    }

    private void OnStillWatchingTimeout(object? state) => OnStillWatchingTimeoutAsync().FireAndForget();

    private async Task OnStillWatchingTimeoutAsync()
    {
        if (_disposed)
            return;

        await InvokeAsync(async () =>
        {
            // The user answered (or moved) between the timer firing and this dispatch.
            if (_stillWatchingTimer is null || !_stillWatchingPrompt)
                return;

            await Dismiss();
            StateHasChanged();
        });
    }

    private void OnOverlayActivity()
    {
        InactivityTracker.RegisterUserActivity();
        StopStillWatchingTimeout();
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
            await InvokeAsync(async () => await PlayNextAsync(automatic: true));
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    private Task ReplayAsync()
    {
        InactivityTracker.RegisterUserActivity();
        PauseCountdown();
        Reset();
        PlayerService.Seek(0);
        PlayerService.Play();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private Task PlayNextNowAsync() => PlayNextAsync(automatic: false);

    private async Task PlayNextAsync(bool automatic)
    {
        if (_nextEpisode is null) return;

        PauseCountdown();

        var nextEpisodeId = _nextEpisode.Id;
        if (automatic)
            InactivityTracker.RegisterAutoAdvance(nextEpisodeId);
        else
            InactivityTracker.RegisterUserActivity();
        var serieId = PlaybackProgressTracker.CurrentSerieId;

        Reset();
        await PopLayerAsync();
        await InvokeAsync(StateHasChanged);

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

        // Same as Play: no track indexes. The server applies serie memory or user preferences.
        await PlayerService.PlayIndexedFileAsync(
            indexedFile.Id,
            videoMetadata.AudioTracks ?? [],
            videoMetadata.SubtitleTracks,
            videoResolution: videoMetadata.VideoResolution,
            thumbnailsUrl: videoMetadata.Thumbnails?.Uri?.ToString(),
            mediaId: nextEpisodeId,
            title: VideoPlayerTitleHelper.FormatEpisode(episodeDto),
            chapters: videoMetadata.Chapters,
            durationSeconds: videoMetadata.Duration.TotalSeconds,
            libraryId: indexedFile.LibraryId,
            filePath: indexedFile.Path,
            originalLanguage: PlayerService.Source.OriginalLanguage);

        StateHasChanged();
    }

    private async Task Dismiss()
    {
        PauseCountdown();
        Reset();
        await ClosePlayerAfterEndAsync();
    }

    private async Task ClosePlayerAfterEndAsync()
    {
        PlaybackProgressTracker.StopTracking();
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
        _stillWatchingPrompt = false;
        StopStillWatchingTimeout();
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
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
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
        _stillWatchingTimer?.Dispose();
        _closeCallbackRef?.Dispose();
        if (_layerActive)
        {
            _ = PopLayerAsync();
        }
    }
}
