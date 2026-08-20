using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;

namespace K7.Clients.MAUI;

/// <summary>
/// Publishes ExoPlayer timeline and first-frame events to MAUI when MediaElement lags behind.
/// </summary>
internal sealed class ExoPlaybackBridge : Java.Lang.Object, IPlayerListener
{
    private const int TimelineIntervalMs = 500;

    private IExoPlayer? _exo;
    private bool _attached;
    private CancellationTokenSource? _tickCts;

    public Action? FirstFrameRendered { get; set; }

    /// <summary>ExoPlayer clock in seconds. MediaElement.Position can freeze after resume/seek.</summary>
    public Action<double>? PositionHeard { get; set; }

    /// <summary>ExoPlayer duration in seconds. MediaElement.Duration is often 0 on demuxed HLS.</summary>
    public Action<double>? DurationHeard { get; set; }

    public void Attach(IExoPlayer exo)
    {
        if (ReferenceEquals(_exo, exo) && _attached)
            return;

        Detach();
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
    }

    public void OnRenderedFirstFrame()
    {
        FirstFrameRendered?.Invoke();
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
        }
        catch
        {
        }
    }
}
