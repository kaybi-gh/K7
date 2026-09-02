using AndroidX.Media3.Common;
using AndroidX.Media3.Common.Text;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;

namespace K7.Clients.MAUI;

/// <summary>
/// Publishes ExoPlayer timeline and first-frame events to MAUI when MediaElement lags behind.
/// </summary>
internal sealed class ExoPlaybackBridge : Java.Lang.Object, IPlayerListener
{
    // 1s is enough for progress/seek UI; 500ms doubled main-thread CurrentTime churn on TV.
    private const int TimelineIntervalMs = 1_000;

    private IExoPlayer? _exo;
    private PlayerView? _playerView;
    private bool _attached;
    private CancellationTokenSource? _tickCts;

    public Action? FirstFrameRendered { get; set; }

    /// <summary>Fired when Exo publishes CurrentTracks (audio/text groups ready to select).</summary>
    public Action? TracksChanged { get; set; }

    /// <summary>ExoPlayer clock in seconds. MediaElement.Position can freeze after resume/seek.</summary>
    public Action<double>? PositionHeard { get; set; }

    /// <summary>ExoPlayer duration in seconds. MediaElement.Duration is often 0 on demuxed HLS.</summary>
    public Action<double>? DurationHeard { get; set; }

    /// <summary>
    /// Exo playbackState, playWhenReady, isPlaying. Used when MediaManager is not an IPlayerListener.
    /// </summary>
    public Action<int, bool, bool>? PlaybackStateHeard { get; set; }

    /// <summary>ExoPlayer error text for the same recovery path as MediaElement.MediaFailed.</summary>
    public Action<string>? PlaybackErrorHeard { get; set; }

    /// <summary>
    /// BufferedPosition in seconds (absolute, same as HTML5 buffered.end). Seek bar and
    /// start-recovery skip need this; MediaElement.Buffered never updates without a listener.
    /// </summary>
    public Action<double>? BufferedHeard { get; set; }

    public void Attach(IExoPlayer exo, PlayerView? playerView = null)
    {
        if (ReferenceEquals(_exo, exo) && _attached)
        {
            _playerView = playerView;
            return;
        }

        Detach();
        _playerView = playerView;
        _exo = exo;
        _attached = true;
        exo.AddListener(this);
        PublishTimeline();
        StartTimelineLoop();
    }

    public void Detach()
    {
        var exo = _exo;
        _attached = false;
        _tickCts?.Cancel();
        _tickCts = null;
        if (exo is not null)
        {
            try
            {
                exo.RemoveListener(this);
            }
            catch
            {
            }
        }

        _exo = null;
        _playerView = null;
    }

    public void OnCues(CueGroup? cueGroup)
    {
        var subtitle = _playerView?.SubtitleView;
        if (subtitle is null)
            return;

        var hasCues = false;
        try
        {
            var cues = cueGroup?.Cues;
            hasCues = cues is not null && cues.Size() > 0;
        }
        catch
        {
        }

        var visibility = hasCues
            ? global::Android.Views.ViewStates.Visible
            : global::Android.Views.ViewStates.Gone;
        subtitle.Visibility = visibility;
        if (!hasCues)
            return;

        // Full-screen GPU SubtitleView over SurfaceView hitchs Amlogic. Software text
        // stays off the HDMI overlay plane.
        subtitle.SetLayerType(global::Android.Views.LayerType.Software, null);
        subtitle.SetBackgroundColor(global::Android.Graphics.Color.Transparent);

        for (var parent = subtitle.Parent as global::Android.Views.View;
             parent is not null && parent is not PlayerView;
             parent = parent.Parent as global::Android.Views.View)
        {
            parent.Visibility = global::Android.Views.ViewStates.Visible;
            parent.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
        }
    }

    public void OnRenderedFirstFrame()
    {
        FirstFrameRendered?.Invoke();
    }

    public void OnTracksChanged(Tracks? tracks)
    {
        _ = tracks;
        TracksChanged?.Invoke();
    }

    public void OnPlaybackStateChanged(int playbackState)
    {
        PublishPlaybackState();
    }

    public void OnIsPlayingChanged(bool isPlaying)
    {
        _ = isPlaying;
        PublishPlaybackState();
    }

    public void OnPlayerError(PlaybackException? error)
    {
        if (error is null)
            return;

        var codeName = PlaybackException.GetErrorCodeName(error.ErrorCode) ?? "(unknown)";
        PlaybackErrorHeard?.Invoke(
            "ExoPlayer " + codeName
            + " code=" + error.ErrorCode
            + " " + (error.Message ?? "(null)"));
    }

    private void PublishPlaybackState()
    {
        var exo = _exo;
        if (exo is null)
            return;

        try
        {
            PlaybackStateHeard?.Invoke(exo.PlaybackState, exo.PlayWhenReady, exo.IsPlaying);
        }
        catch
        {
        }
    }

    private void StartTimelineLoop()
    {
        _tickCts?.Cancel();
        var cts = new CancellationTokenSource();
        _tickCts = cts;
        var ct = cts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimelineIntervalMs, ct);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_attached)
                        PublishTimeline();
                });
            }
        });
    }

    private void PublishTimeline()
    {
        var exo = _exo;
        if (exo is null)
            return;

        try
        {
            var durMs = exo.Duration;
            if (durMs > 0 && durMs < 864_000_000_000L)
                DurationHeard?.Invoke(durMs / 1000.0);

            var posMs = exo.CurrentPosition;
            if (posMs > 0)
                PositionHeard?.Invoke(posMs / 1000.0);

            var bufferedMs = exo.BufferedPosition;
            if (bufferedMs > 0)
                BufferedHeard?.Invoke(bufferedMs / 1000.0);
        }
        catch
        {
        }
    }
}
