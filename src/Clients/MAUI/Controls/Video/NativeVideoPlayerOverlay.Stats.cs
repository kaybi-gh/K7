using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using Timer = System.Timers.Timer;

namespace K7.Clients.MAUI.Controls.Video;

public sealed partial class NativeVideoPlayerOverlay
{
    private readonly NativePlaybackStatsHud _statsHud = new();
    private Timer? _statsTimer;
    private bool _canShowPlaybackStats;
    private bool _statsEnabled;

    private void InitializePlaybackStats()
    {
        _settings.StatsToggled += OnStatsToggled;
    }

    private async Task LoadPlaybackStatsAsync()
    {
        _canShowPlaybackStats = false;
        if (_featureAccess is not null)
        {
            try
            {
                _canShowPlaybackStats = await _featureAccess.HasCapabilityAsync(Capability.CanAccessAdmin);
            }
            catch
            {
                _canShowPlaybackStats = false;
            }
        }

        _statsEnabled = false;
        if (_canShowPlaybackStats && _deviceStorage is not null)
        {
            try
            {
                _statsEnabled = _deviceStorage.Get(PreferenceKeys.VIDEO_PLAYBACK_NERD_STATS, false);
            }
            catch
            {
                _statsEnabled = false;
            }
        }

        _settings.ShowStatsToggle = _canShowPlaybackStats;
        _settings.StatsEnabled = _statsEnabled;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { _settings.Rebuild(); } catch { /* ignore */ }
            ApplyPlaybackStatsHud();
        });
    }

    private void OnStatsToggled(object? sender, bool enabled)
    {
        _statsEnabled = enabled && _canShowPlaybackStats;
        _settings.StatsEnabled = _statsEnabled;
        try
        {
            _deviceStorage?.Set(PreferenceKeys.VIDEO_PLAYBACK_NERD_STATS, _statsEnabled);
        }
        catch
        {
        }

        ApplyPlaybackStatsHud();
    }

    private void ApplyPlaybackStatsHud()
    {
        var show = IsVisible && _canShowPlaybackStats && _statsEnabled;
        if (show)
        {
            AttachStatsHud();
            _statsHud.IsVisible = true;
            RefreshPlaybackStatsHud();
            StartStatsTimer();
        }
        else
        {
            DetachStatsHud();
        }

        SyncTvSurfaceComposition();
    }

    private void AttachStatsHud()
    {
        if (Parent is not Grid host)
            return;

        if (ReferenceEquals(_statsHud.Parent, host))
            return;

        if (_statsHud.Parent is Grid oldHost)
            oldHost.Children.Remove(_statsHud);

        _statsHud.ZIndex = 6;
        host.Children.Add(_statsHud);
    }

    private void DetachStatsHud()
    {
        StopStatsTimer();
        _statsHud.IsVisible = false;
        if (_statsHud.Parent is Grid oldHost)
            oldHost.Children.Remove(_statsHud);
    }

    private void StartStatsTimer()
    {
        if (_statsTimer is not null)
            return;

        _statsTimer = new Timer(1000) { AutoReset = true };
        _statsTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshPlaybackStatsHud);
        _statsTimer.Start();
    }

    private void StopStatsTimer()
    {
        _statsTimer?.Stop();
        _statsTimer?.Dispose();
        _statsTimer = null;
    }

    private void RefreshPlaybackStatsHud()
    {
        if (!_statsHud.IsVisible)
            return;

        NativePlaybackStatsSnapshot snapshot;
#if ANDROID
        snapshot = Platforms.Android.AndroidExoPlaybackStats.Capture(_player);
#else
        snapshot = CaptureFallbackStats();
#endif
        snapshot = NativePlaybackStatsFormatting.WithDecision(
            snapshot,
            _player.Source?.StreamDecision);
        _statsHud.SetSnapshot(snapshot);
    }

    private NativePlaybackStatsSnapshot CaptureFallbackStats()
    {
        var url = _player.Source?.Url;
        var mime = _player.Source?.MimeType;
        var quality = _player.SelectedQuality;
        return new NativePlaybackStatsSnapshot
        {
            PlayMethod = NativePlaybackStatsFormatting.PlayMethod(
                url,
                mime,
                quality?.IsOriginal ?? !StreamingSourceKind.IsHls(mime, url)),
            Quality = quality?.Label ?? "",
            Buffer = NativePlaybackStatsFormatting.FormatBuffer(_player.BufferedTime)
        };
    }
}
