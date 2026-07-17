using AVFoundation;
using CoreMedia;
using Foundation;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

/// <summary>
/// iOS audio playback service using AVPlayer.
/// Bridges IAudioPlayerService transport events to the native AVPlayer
/// and reports playback state back.
/// </summary>
public class NativeAudioService : NSObject, IDisposable
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IK7ServerService _k7ServerService;
    private AVPlayer? _player;
    private NSObject? _timeObserver;
    private NSObject? _endObserver;
    private AVPlayerItem? _observedItem;
    private bool _updatingFromPlayer;

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

        _audioPlayerService.SourceChanged += OnSourceChanged;
        _audioPlayerService.PlayRequested += OnPlayRequested;
        _audioPlayerService.PauseRequested += OnPauseRequested;
        _audioPlayerService.StopRequested += OnStopRequested;
        _audioPlayerService.SeekRequested += OnSeekRequested;
        _audioPlayerService.VolumeChangeRequested += OnVolumeChanged;
        _audioPlayerService.MuteRequested += OnMuteRequested;
        _audioPlayerService.UnmuteRequested += OnUnmuteRequested;

        StartPositionObserver();
    }

    private static void ConfigureAudioSession()
    {
        var session = AVAudioSession.SharedInstance();
        session.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.DefaultToSpeaker);
        session.SetActive(true);
    }

    private void OnSourceChanged(PlayerSource source)
    {
        if (_player is null || string.IsNullOrEmpty(source.Url)) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            RemoveEndObserver();
            RemoveItemStatusObserver();

            var url = CreateAuthenticatedUrl(source.Url);
            var playerItem = new AVPlayerItem(AVAsset.FromUrl(url));
            _player.ReplaceCurrentItemWithPlayerItem(playerItem);
            _player.Play();

            _updatingFromPlayer = true;
            _audioPlayerService.PlaybackState = PlaybackState.Buffering;
            _updatingFromPlayer = false;

            ObserveItemStatus(playerItem);
            AddEndObserver();
        });
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
            if (_audioPlayerService is null) return;

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
        if (_player is not null)
            _player.Volume = (float)volume;
        return Task.CompletedTask;
    }

    private Task OnMuteRequested()
    {
        if (_player is not null)
            _player.Muted = true;
        return Task.CompletedTask;
    }

    private Task OnUnmuteRequested()
    {
        if (_player is not null)
            _player.Muted = false;
        return Task.CompletedTask;
    }

    private NSUrl CreateAuthenticatedUrl(string url)
    {
        // AVPlayer handles auth via HTTP headers on AVURLAsset, but for simplicity
        // use the URL directly since the streaming endpoint uses token-based auth in query params
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

        RemoveEndObserver();
        RemoveItemStatusObserver();

        if (_timeObserver is not null && _player is not null)
        {
            _player.RemoveTimeObserver(_timeObserver);
            _timeObserver = null;
        }

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
