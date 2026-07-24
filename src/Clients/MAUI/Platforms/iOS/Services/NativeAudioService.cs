using AVFoundation;
using CoreMedia;
using Foundation;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

/// <summary>
/// iOS audio playback service using dual AVPlayers for true overlapping crossfade.
/// Bridges IAudioPlayerService transport events and reports playback state back.
/// </summary>
public class NativeAudioService : NSObject, IDisposable
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IosAudioEqualizer _equalizer = new();
    private AVPlayer? _player;
    private AVPlayer? _crossfadePlayer;
    private NSObject? _timeObserver;
    private NSObject? _endObserver;
    private AVPlayerItem? _observedItem;
    private volatile bool _updatingFromPlayer;
    private CancellationTokenSource? _fadeCts;
    private bool _crossfadeInProgress;
    private string? _gaplessPrebufferedUrl;
    private float _loudnessLinearGain = 1f;
    private float _userVolume = 1f;

    public NativeAudioService(IAudioPlayerService audioPlayerService, IK7ServerService k7ServerService)
    {
        _audioPlayerService = audioPlayerService;
        _k7ServerService = k7ServerService;
        Initialize();
    }

    private void Initialize()
    {
        ConfigureAudioSession();

        _player = new AVPlayer { ActionAtItemEnd = AVPlayerActionAtItemEnd.None };
        _crossfadePlayer = new AVPlayer { ActionAtItemEnd = AVPlayerActionAtItemEnd.None };
        _userVolume = (float)_audioPlayerService.Volume;

        _audioPlayerService.SourceChanged += OnSourceChanged;
        _audioPlayerService.PlayRequested += OnPlayRequested;
        _audioPlayerService.PauseRequested += OnPauseRequested;
        _audioPlayerService.StopRequested += OnStopRequested;
        _audioPlayerService.SeekRequested += OnSeekRequested;
        _audioPlayerService.VolumeChangeRequested += OnVolumeChanged;
        _audioPlayerService.MuteRequested += OnMuteRequested;
        _audioPlayerService.UnmuteRequested += OnUnmuteRequested;
        _audioPlayerService.FadeOutRequested += OnFadeOutRequested;
        _audioPlayerService.FadeResetRequested += OnFadeResetRequested;
        _audioPlayerService.CrossfadeRequested += OnCrossfadeRequested;
        _audioPlayerService.GaplessPrebufferRequested += OnGaplessPrebufferRequested;
        _audioPlayerService.LoudnessSettingsChanged += OnLoudnessSettingsChanged;
        _audioPlayerService.CurrentTrackChanged += OnCurrentTrackChanged;
        _audioPlayerService.EqSettingsChanged += OnEqSettingsChanged;

        RefreshLoudnessGain();
        _equalizer.UpdateSettings(_audioPlayerService.EqEnabled, _audioPlayerService.EqBands);
        StartPositionObserver();
    }

    private void OnEqSettingsChanged()
        => _equalizer.UpdateSettings(_audioPlayerService.EqEnabled, _audioPlayerService.EqBands);

    private static void ConfigureAudioSession()
    {
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.DefaultToSpeaker);
        session.SetActive(true);
    }

    private float PeakVolume => _userVolume * _loudnessLinearGain;

    private void OnLoudnessSettingsChanged() => RefreshLoudnessGain(applyToPlayer: true);

    private void OnCurrentTrackChanged(AudioQueueItem? _) => RefreshLoudnessGain(applyToPlayer: !_crossfadeInProgress);

    private void RefreshLoudnessGain(bool applyToPlayer = false)
    {
        var track = _audioPlayerService.CurrentTrack;
        var linear = LoudnessGainHelper.ComputeLinearGain(
            _audioPlayerService.LoudnessEnabled,
            _audioPlayerService.LoudnessTargetLufs,
            _audioPlayerService.LoudnessPreampDb,
            track?.LoudnessLufs,
            track?.ReplayGainTrackGain);
        _loudnessLinearGain = (float)LoudnessGainHelper.ApplySoftLimiter(linear, _audioPlayerService.LimiterEnabled);

        if (applyToPlayer && !_crossfadeInProgress && _player is not null)
            MainThread.BeginInvokeOnMainThread(() => _player.Volume = PeakVolume);
    }

    private void OnSourceChanged(PlayerSource source)
    {
        if (_player is null || string.IsNullOrEmpty(source.Url)) return;
        if (_crossfadeInProgress) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_crossfadePlayer is not null
                && string.Equals(_gaplessPrebufferedUrl, source.Url, StringComparison.Ordinal)
                && _crossfadePlayer.CurrentItem is not null)
            {
                _ = PromoteGaplessAsync(source);
                return;
            }

            ApplySource(_player, source, PeakVolume, observe: true);
            _gaplessPrebufferedUrl = null;
        });
    }

    private void ApplySource(AVPlayer player, PlayerSource source, float startVolume, bool observe)
    {
        if (string.IsNullOrEmpty(source.Url)) return;

        if (observe)
        {
            RemoveEndObserver();
            RemoveItemStatusObserver();
        }

        var url = CreateAuthenticatedUrl(source.Url);
        var playerItem = new AVPlayerItem(AVAsset.FromUrl(url));
        player.Volume = startVolume;
        player.ReplaceCurrentItemWithPlayerItem(playerItem);
        player.Play();
        _equalizer.AttachToPlayerItem(playerItem);

        if (observe)
        {
            _updatingFromPlayer = true;
            _audioPlayerService.PlaybackState = PlaybackState.Buffering;
            _updatingFromPlayer = false;
            ObserveItemStatus(playerItem);
            AddEndObserver();
        }
    }

    private async Task OnGaplessPrebufferRequested(PlayerSource source)
    {
        if (_crossfadePlayer is null || string.IsNullOrEmpty(source.Url)) return;

        _gaplessPrebufferedUrl = source.Url;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var url = CreateAuthenticatedUrl(source.Url);
            var item = new AVPlayerItem(AVAsset.FromUrl(url));
            _crossfadePlayer.Volume = 0f;
            _crossfadePlayer.ReplaceCurrentItemWithPlayerItem(item);
            _equalizer.AttachToPlayerItem(item);
            _crossfadePlayer.Pause();
        });
    }

    private async Task PromoteGaplessAsync(PlayerSource source)
    {
        if (_player is null || _crossfadePlayer is null) return;

        var peak = PeakVolume;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _crossfadePlayer.Volume = peak;
            _crossfadePlayer.Play();
        });

        await MainThread.InvokeOnMainThreadAsync(() => ApplySource(_player, source, 0f, observe: true));
        await WaitUntilItemReadyAsync(_player, CancellationToken.None);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _player.Volume = peak;
            _player.Play();
            _crossfadePlayer.Pause();
            _crossfadePlayer.ReplaceCurrentItemWithPlayerItem(null);
            _crossfadePlayer.Volume = 0f;
            _gaplessPrebufferedUrl = null;
        });
    }

    private async Task OnCrossfadeRequested(PlayerSource source, double durationSeconds)
    {
        if (_player is null || _crossfadePlayer is null || string.IsNullOrEmpty(source.Url)) return;

        _crossfadeInProgress = true;
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;
        var peak = PeakVolume;

        try
        {
            var alreadyPrepared = string.Equals(_gaplessPrebufferedUrl, source.Url, StringComparison.Ordinal);
            if (!alreadyPrepared)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var url = CreateAuthenticatedUrl(source.Url);
                    var item = new AVPlayerItem(AVAsset.FromUrl(url));
                    _crossfadePlayer.Volume = 0f;
                    _crossfadePlayer.ReplaceCurrentItemWithPlayerItem(item);
                    _equalizer.AttachToPlayerItem(item);
                });
                await WaitUntilItemReadyAsync(_crossfadePlayer, ct);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _crossfadePlayer.Volume = 0f;
                _crossfadePlayer.Play();
            });

            await EqualPowerCrossfadeAsync(_player, _crossfadePlayer, Math.Max(0.25, durationSeconds), peak, ct);

            var handoffSeconds = _crossfadePlayer.CurrentTime.Seconds;
            await MainThread.InvokeOnMainThreadAsync(() => ApplySource(_player, source, 0f, observe: true));
            if (handoffSeconds > 0)
                await MainThread.InvokeOnMainThreadAsync(() => _player.Seek(CMTime.FromSeconds(handoffSeconds, 1)));

            await WaitUntilItemReadyAsync(_player, ct);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _player.Volume = peak;
                _player.Play();
                _crossfadePlayer.Pause();
                _crossfadePlayer.ReplaceCurrentItemWithPlayerItem(null);
                _crossfadePlayer.Volume = 0f;
            });

            _gaplessPrebufferedUrl = null;
        }
        catch (System.OperationCanceledException)
        {
        }
        finally
        {
            _crossfadeInProgress = false;
            _audioPlayerService.NotifyCrossfadeCompleted();
        }
    }

    private static async Task WaitUntilItemReadyAsync(AVPlayer player, CancellationToken ct)
    {
        for (var i = 0; i < 100; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (player.CurrentItem?.Status == AVPlayerItemStatus.ReadyToPlay)
                return;
            await Task.Delay(50, ct);
        }
    }

    private async Task EqualPowerCrossfadeAsync(
        AVPlayer outgoing,
        AVPlayer incoming,
        double durationSeconds,
        float peakVolume,
        CancellationToken ct)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(durationSeconds * 20));
        var stepMs = Math.Max(1, (int)(durationSeconds * 1000 / steps));

        for (var i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ratio = i / (float)steps;
            var fadeOut = (float)(Math.Cos(ratio * Math.PI / 2.0) * peakVolume);
            var fadeIn = (float)(Math.Sin(ratio * Math.PI / 2.0) * peakVolume);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                outgoing.Volume = fadeOut;
                incoming.Volume = fadeIn;
            });
            await Task.Delay(stepMs, ct);
        }
    }

    private void ObserveItemStatus(AVPlayerItem item)
    {
        _observedItem = item;
        item.AddObserver(this, "status", NSKeyValueObservingOptions.New, nint.Zero);
    }

    private void RemoveItemStatusObserver()
    {
        if (_observedItem is null)
            return;

        _observedItem.RemoveObserver(this, "status");
        _observedItem = null;
    }

    public override void ObserveValue(NSString keyPath, NSObject ofObject, NSDictionary? change, nint context)
    {
        if (keyPath == "status" && ofObject is AVPlayerItem item)
        {
            RemoveItemStatusObserver();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (item.Status == AVPlayerItemStatus.ReadyToPlay)
                {
                    _updatingFromPlayer = true;
                    _audioPlayerService.Duration = item.Duration.Seconds;
                    _audioPlayerService.PlaybackState = PlaybackState.Playing;
                    _updatingFromPlayer = false;
                }
                else if (item.Status == AVPlayerItemStatus.Failed)
                {
                    System.Diagnostics.Debug.WriteLine($"[K7-iOS-Audio] Playback failed: {item.Error?.LocalizedDescription}");
                    _updatingFromPlayer = true;
                    _audioPlayerService.PlaybackState = PlaybackState.Idle;
                    _updatingFromPlayer = false;
                }
            });
        }
    }

    private void AddEndObserver()
    {
        _endObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            AVPlayerItem.DidPlayToEndTimeNotification,
            _ =>
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _updatingFromPlayer = true;
                    await _audioPlayerService.OnTrackEndedAsync();
                    _updatingFromPlayer = false;
                });
            },
            _player?.CurrentItem);
    }

    private void RemoveEndObserver()
    {
        if (_endObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_endObserver);
            _endObserver = null;
        }
    }

    private void StartPositionObserver()
    {
        if (_player is null) return;

        var interval = CMTime.FromSeconds(0.5, 1);
        _timeObserver = _player.AddPeriodicTimeObserver(interval, null, time =>
        {
            _updatingFromPlayer = true;
            _audioPlayerService.CurrentTime = time.Seconds;

            if (_player.CurrentItem is not null)
            {
                var loadedRanges = _player.CurrentItem.LoadedTimeRanges;
                if (loadedRanges.Length > 0)
                {
                    var range = loadedRanges[0].CMTimeRangeValue;
                    _audioPlayerService.BufferedTime = range.Start.Seconds + range.Duration.Seconds;
                }
            }

            _updatingFromPlayer = false;
        });
    }

    private Task OnPlayRequested()
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() => _player?.Play());
        return Task.CompletedTask;
    }

    private Task OnPauseRequested()
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() => _player?.Pause());
        return Task.CompletedTask;
    }

    private Task OnStopRequested()
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _player?.Pause();
            _player?.Seek(CMTime.Zero);
        });
        return Task.CompletedTask;
    }

    private Task OnSeekRequested(double positionSeconds)
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() =>
            _player?.Seek(CMTime.FromSeconds(positionSeconds, 1)));
        return Task.CompletedTask;
    }

    private Task OnVolumeChanged(double volume)
    {
        _userVolume = (float)volume;
        if (_player is not null && !_crossfadeInProgress)
            _player.Volume = PeakVolume;
        return Task.CompletedTask;
    }

    private async Task OnFadeOutRequested(double durationSeconds)
    {
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;
        var startVolume = _player?.Volume ?? PeakVolume;

        try
        {
            await FadePlayerVolumeAsync(startVolume, 0f, Math.Max(0.25, durationSeconds), ct);
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private async Task FadePlayerVolumeAsync(float from, float to, double durationSeconds, CancellationToken ct)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(durationSeconds * 20));
        var stepMs = Math.Max(1, (int)(durationSeconds * 1000 / steps));

        for (var i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = i / (float)steps;
            var volume = from + ((to - from) * t);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_player is not null)
                    _player.Volume = volume;
            });
            await Task.Delay(stepMs, ct);
        }
    }

    private Task OnFadeResetRequested()
    {
        _fadeCts?.Cancel();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_player is not null)
                _player.Volume = PeakVolume;
        });
        return Task.CompletedTask;
    }

    private Task OnMuteRequested()
    {
        if (_player is not null)
            _player.Muted = true;
        if (_crossfadePlayer is not null)
            _crossfadePlayer.Muted = true;
        return Task.CompletedTask;
    }

    private Task OnUnmuteRequested()
    {
        if (_player is not null)
            _player.Muted = false;
        if (_crossfadePlayer is not null)
            _crossfadePlayer.Muted = false;
        return Task.CompletedTask;
    }

    private NSUrl CreateAuthenticatedUrl(string url)
    {
        // Streaming endpoint uses token-based auth in query params.
        return new NSUrl(url);
    }

    public void Cleanup()
    {
        _audioPlayerService.SourceChanged -= OnSourceChanged;
        _audioPlayerService.PlayRequested -= OnPlayRequested;
        _audioPlayerService.PauseRequested -= OnPauseRequested;
        _audioPlayerService.StopRequested -= OnStopRequested;
        _audioPlayerService.SeekRequested -= OnSeekRequested;
        _audioPlayerService.VolumeChangeRequested -= OnVolumeChanged;
        _audioPlayerService.MuteRequested -= OnMuteRequested;
        _audioPlayerService.UnmuteRequested -= OnUnmuteRequested;
        _audioPlayerService.FadeOutRequested -= OnFadeOutRequested;
        _audioPlayerService.FadeResetRequested -= OnFadeResetRequested;
        _audioPlayerService.CrossfadeRequested -= OnCrossfadeRequested;
        _audioPlayerService.GaplessPrebufferRequested -= OnGaplessPrebufferRequested;
        _audioPlayerService.LoudnessSettingsChanged -= OnLoudnessSettingsChanged;
        _audioPlayerService.CurrentTrackChanged -= OnCurrentTrackChanged;
        _audioPlayerService.EqSettingsChanged -= OnEqSettingsChanged;

        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = null;

        RemoveEndObserver();
        RemoveItemStatusObserver();

        if (_timeObserver is not null && _player is not null)
        {
            _player.RemoveTimeObserver(_timeObserver);
            _timeObserver = null;
        }

        _equalizer.Dispose();
        _crossfadePlayer?.Dispose();
        _crossfadePlayer = null;
        _player?.Dispose();
        _player = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Cleanup();

        base.Dispose(disposing);
    }
}
