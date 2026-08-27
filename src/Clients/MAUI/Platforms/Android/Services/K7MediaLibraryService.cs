using System.Net.Http.Headers;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Log = Android.Util.Log;
using Resource = K7.Clients.MAUI.Resource;

#pragma warning disable XAOBS001 // ResolvableFuture is the only way to create IListenableFuture in .NET Android bindings

namespace K7.Clients.MAUI.Platforms.Android.Services;

[Service(
    Name = "com.k7.maui.K7MediaLibraryService",
    ForegroundServiceType = ForegroundService.TypeMediaPlayback,
    Exported = true)]
[IntentFilter(["androidx.media3.session.MediaLibraryService",
    "android.media.browse.MediaBrowserService"],
    Categories = ["android.intent.category.DEFAULT"])]
public class K7MediaLibraryService : MediaLibraryService,
    MediaLibraryService.MediaLibrarySession.ICallback,
    IPlayerListener
{
    private const string Tag = "K7-MediaLibrary";
    private const string RootId = "k7_root";

    private IExoPlayer? _player;
    private IExoPlayer? _crossfadePlayer;
    private DefaultMediaSourceFactory? _mediaSourceFactory;
    private AudioAttributes? _audioAttributes;
    private K7ForwardingPlayer? _forwardingPlayer;
    private K7VideoSessionPlayer? _videoSessionPlayer;
    private MediaLibrarySession? _session;
    private MediaSession? _videoSession;

    private IMediaBrowseService? _mediaBrowseService;
    private IAudioPlayerService? _audioPlayerService;
    private IPlayerService? _playerService;
    private IStreamUriService? _streamUriService;
    private IK7ServerService? _k7ServerService;
    private DefaultHttpDataSource.Factory? _httpDataSourceFactory;
    private readonly AndroidAudioEqualizer _audioEqualizer = new();
    private CancellationTokenSource? _fadeCts;
    private bool _crossfadeInProgress;
    private string? _gaplessPrebufferedUrl;
    private float _loudnessLinearGain = 1f;

    private volatile bool _updatingFromPlayer;
    private volatile bool _isVideoMode;
    private bool _videoSessionAdded;
    private bool _syncingFromExoPlayer;
    private IList<MediaItem>? _resolvedQueueMediaItems;
    private readonly HashSet<Guid> _radioMediaIdsOnPlayer = [];
    private readonly SemaphoreSlim _radioPlaylistSync = new(1, 1);
    private bool _radioAwaitingMedia3Playlist;
    private CancellationTokenSource? _radioSyncDebounceCts;
    private static readonly TimeSpan RadioPlaylistSyncDebounce = TimeSpan.FromMilliseconds(400);

    public override void OnCreate()
    {
        base.OnCreate();
        Log.Info(Tag, "K7MediaLibraryService created");

        try
        {
            InitializeService();
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"K7MediaLibraryService initialization failed: {ex}");
        }
    }

    private void InitializeService()
    {
        var services = IPlatformApplication.Current?.Services;
        if (services is null)
        {
            Log.Error(Tag, "DI container not available");
            return;
        }

        _mediaBrowseService = services.GetRequiredService<IMediaBrowseService>();
        _audioPlayerService = services.GetRequiredService<IAudioPlayerService>();
        _playerService = services.GetRequiredService<IPlayerService>();
        _streamUriService = services.GetRequiredService<IStreamUriService>();
        _k7ServerService = services.GetRequiredService<IK7ServerService>();

        // Ensure HttpClient BaseAddress is set (service may start before App.xaml.cs runs)
        EnsureServerBaseAddress();

        _httpDataSourceFactory = new DefaultHttpDataSource.Factory();
        UpdateAuthHeaders();

        var dataSourceFactory = new DefaultDataSource.Factory(this, _httpDataSourceFactory);
        _mediaSourceFactory = new DefaultMediaSourceFactory(this as Context);
        _mediaSourceFactory.SetDataSourceFactory(dataSourceFactory);

        _audioAttributes = new AudioAttributes.Builder()!
            .SetUsage((int)global::Android.Media.AudioUsageKind.Media)!
            .SetContentType((int)global::Android.Media.AudioContentType.Music)!
            .Build()!;

        _player = CreateExoPlayer(handleAudioFocus: true);
        _crossfadePlayer = CreateExoPlayer(handleAudioFocus: false);

        _player.AddListener(this);

        _forwardingPlayer = new K7ForwardingPlayer(
            _player,
            hasNext: () => _audioPlayerService.CurrentIndex < _audioPlayerService.Queue.Count - 1,
            hasPrevious: () => _audioPlayerService.CurrentIndex > 0 || _audioPlayerService.CurrentTime > 3,
            onSeekToNext: () => _ = _audioPlayerService.NextAsync(),
            onSeekToPrevious: () => _ = _audioPlayerService.PreviousAsync());

        _videoSessionPlayer = new K7VideoSessionPlayer(MainLooper!, _playerService);

        // PendingIntent to open the app when notification is tapped
        var launchIntent = new Intent(this, typeof(MainActivity));
        launchIntent.SetAction(Intent.ActionMain);
        launchIntent.AddCategory(Intent.CategoryLauncher);
        launchIntent.PutExtra("open_fullscreen_player", true);
        var pendingIntent = PendingIntent.GetActivity(
            this, 0, launchIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;

        // Android Auto resets browse/queue scroll on every playback-state push.
        // Disable Media3 periodic position updates so the queue stays scrollable
        // (workaround for AA companion bug; see androidx/media#2192).
        _session = new MediaLibrarySession.Builder(this, _forwardingPlayer, this)!
            .SetSessionActivity(pendingIntent)!
            .SetPeriodicPositionUpdateEnabledBuilder(false)!
            .Build()!;

        _videoSession = new MediaSession.Builder(this, _videoSessionPlayer)!
            .SetSessionActivity(pendingIntent)!
            .SetId("k7_video")!
            .Build()!;

        // Explicitly register the session with the base MediaSessionService so
        // the notification manager observes player state changes. Without this,
        // onGetSession() returns null during base.OnCreate() (session not yet created).
        AddSession(_session);

        // Set notification provider with custom channel
        var notifBuilder = new DefaultMediaNotificationProvider.Builder(this);
        notifBuilder.SetNotificationId(1001);
        notifBuilder.SetChannelId("k7_media_playback");
        var notificationProvider = notifBuilder.Build()!;
        notificationProvider.SetSmallIcon(Resource.Drawable.ic_notification);
        SetMediaNotificationProvider(notificationProvider);

        SubscribeToAudioPlayerEvents();
        SubscribeToVideoPlayerEvents();

        if (_audioPlayerService is not null)
            _audioEqualizer.UpdateSettings(_audioPlayerService.EqEnabled, _audioPlayerService.EqBands);
    }

    public override MediaLibrarySession? OnGetSessionFromMediaLibraryService(
        MediaSession.ControllerInfo? controllerInfo)
    {
        return _session;
    }

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo)
    {
        return _session;
    }

    public override void OnDestroy()
    {
        UnsubscribeFromAudioPlayerEvents();
        UnsubscribeFromVideoPlayerEvents();
        CancelRadioPlaylistSyncDebounce();
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = null;
        _audioEqualizer.Dispose();
        _radioPlaylistSync.Dispose();
        _videoSession?.Release();
        _session?.Release();
        _player?.RemoveListener(this);
        _crossfadePlayer?.RemoveListener(this);
        _player?.Release();
        _crossfadePlayer?.Stop();
        _crossfadePlayer?.Release();
        _forwardingPlayer = null;
        _videoSession = null;
        _session = null;
        _player = null;
        _crossfadePlayer = null;
        base.OnDestroy();
        Log.Info(Tag, "K7MediaLibraryService destroyed");
    }

    private IExoPlayer CreateExoPlayer(bool handleAudioFocus)
    {
#pragma warning disable CS0618 // IMediaSourceFactory marked obsolete in .NET Android bindings but is the correct Media3 API
        return new ExoPlayerBuilder(this)!
            .SetMediaSourceFactory(_mediaSourceFactory as AndroidX.Media3.ExoPlayer.Source.IMediaSourceFactory)!
            .SetAudioAttributes(_audioAttributes!, handleAudioFocus)!
            .SetHandleAudioBecomingNoisy(true)!
            .SetWakeMode(AndroidX.Media3.Common.C.WakeModeLocal)!
            .Build()!;
#pragma warning restore CS0618
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return base.OnBind(intent);
    }

    // --- IPlayerListener: ExoPlayer state -> IAudioPlayerService / IPlayerService ---

    public void OnPlaybackStateChanged(int playbackState)
    {
        if (_isVideoMode) return; // Video mode ignores ExoPlayer playback state for audio
        if (_audioPlayerService is null) return;

        try
        {
            // Player state constants: Idle=1, Buffering=2, Ready=3, Ended=4
            // Single-item Blazor playback has no ExoPlayer playlist, so STATE_ENDED
            // must drive queue advance (same as iOS DidPlayToEndTime -> OnTrackEndedAsync).
            // Multi-item Android Auto playlists auto-advance via OnMediaItemTransition instead;
            // STATE_ENDED only fires after the last playlist item.
            if (playbackState == 3)
                TryCompleteRadioPlaylistHandoff();

            if (playbackState == 4)
            {
                if (_crossfadeInProgress)
                {
                    // Outgoing track finished during the blend. Incoming is already
                    // audible; do not reload it onto this player (that cut is the bug).
                    TransferPlaybackFocusToIncoming();
                    return;
                }

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await _audioPlayerService.OnTrackEndedAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Tag, $"OnTrackEndedAsync failed: {ex.Message}");
                    }
                });
                return;
            }

            _updatingFromPlayer = true;
            _audioPlayerService.PlaybackState = playbackState switch
            {
                3 => _player?.IsPlaying == true
                    ? PlaybackState.Playing
                    : PlaybackState.Paused,
                2 => PlaybackState.Buffering,
                _ => PlaybackState.Idle,
            };
            _updatingFromPlayer = false;
        }
        catch (Exception ex)
        {
            _updatingFromPlayer = false;
            Log.Error(Tag, $"OnPlaybackStateChanged failed: {ex.Message}");
        }
    }

    public void OnIsPlayingChanged(bool isPlaying)
    {
        if (_isVideoMode) return; // Video mode handled by K7VideoSessionPlayer
        if (_audioPlayerService is null) return;

        // Track end reports isPlaying=false alongside STATE_ENDED; let OnTrackEndedAsync
        // own the transition so we do not overwrite with Paused and race NextAsync.
        // During crossfade the outgoing player ending must not flip UI to Paused.
        if (!isPlaying && (_crossfadeInProgress || _player?.PlaybackState == 4))
            return;

        try
        {
            _updatingFromPlayer = true;
            _audioPlayerService.PlaybackState = isPlaying
                ? PlaybackState.Playing
                : PlaybackState.Paused;
            _updatingFromPlayer = false;

            if (isPlaying)
                StartPositionUpdates();
            else
                StopPositionUpdates();
        }
        catch (Exception ex)
        {
            _updatingFromPlayer = false;
            Log.Error(Tag, $"OnIsPlayingChanged failed: {ex.Message}");
        }
    }

    public void OnPlayerError(PlaybackException? error)
    {
        Log.Error(Tag, $"ExoPlayer error: {error?.Message} (code={error?.ErrorCode})");
        if (_crossfadeInProgress)
            return;

        if (_audioPlayerService is not null)
        {
            _updatingFromPlayer = true;
            _audioPlayerService.PlaybackState = PlaybackState.Idle;
            _updatingFromPlayer = false;
        }
    }

    public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
    {
        if (_isVideoMode) return;
        if (_audioPlayerService is null || _player is null) return;

        var duration = _player.Duration;
        if (duration > 0)
        {
            _updatingFromPlayer = true;
            _audioPlayerService.Duration = duration / 1000.0;
            _updatingFromPlayer = false;
        }

        // Auto-advance (reason=1): ExoPlayer moved to next track in playlist.
        // Sync AudioPlayerService without triggering OnSourceChanged.
        // Skip while soft-crossfading - the queue index was already advanced.
        if (reason == 1 && !_crossfadeInProgress
            && _resolvedQueueMediaItems is not null && _resolvedQueueMediaItems.Count > 1)
        {
            TryCompleteRadioPlaylistHandoff();
            _ = Task.Run(async () =>
            {
                _syncingFromExoPlayer = true;
                try
                {
                    await _audioPlayerService.NextAsync();
                }
                finally
                {
                    _syncingFromExoPlayer = false;
                }
            });
        }
    }

    public void OnAudioSessionIdChanged(int audioSessionId)
    {
        if (_isVideoMode)
        {
            _audioEqualizer.Detach();
            return;
        }

        _audioEqualizer.Attach(audioSessionId);
    }

    // --- IAudioPlayerService events -> ExoPlayer ---

    private void SubscribeToAudioPlayerEvents()
    {
        if (_audioPlayerService is null) return;

        _audioPlayerService.SourceChanged += OnSourceChanged;
        _audioPlayerService.PlayRequested += OnPlayRequested;
        _audioPlayerService.PauseRequested += OnPauseRequested;
        _audioPlayerService.StopRequested += OnStopRequested;
        _audioPlayerService.SeekRequested += OnSeekRequested;
        _audioPlayerService.CurrentTrackChanged += OnCurrentTrackChanged;
        _audioPlayerService.QueueChanged += OnQueueChanged;
        _audioPlayerService.ActiveRadioChanged += OnActiveRadioChanged;
        _audioPlayerService.EqSettingsChanged += OnEqSettingsChanged;
        _audioPlayerService.FadeOutRequested += OnFadeOutRequested;
        _audioPlayerService.FadeResetRequested += OnFadeResetRequested;
        _audioPlayerService.CrossfadeRequested += OnCrossfadeRequested;
        _audioPlayerService.GaplessPrebufferRequested += OnGaplessPrebufferRequested;
        _audioPlayerService.LoudnessSettingsChanged += OnLoudnessSettingsChanged;
        RefreshLoudnessGain();
    }

    private void UnsubscribeFromAudioPlayerEvents()
    {
        if (_audioPlayerService is null) return;

        _audioPlayerService.SourceChanged -= OnSourceChanged;
        _audioPlayerService.PlayRequested -= OnPlayRequested;
        _audioPlayerService.PauseRequested -= OnPauseRequested;
        _audioPlayerService.StopRequested -= OnStopRequested;
        _audioPlayerService.SeekRequested -= OnSeekRequested;
        _audioPlayerService.CurrentTrackChanged -= OnCurrentTrackChanged;
        _audioPlayerService.QueueChanged -= OnQueueChanged;
        _audioPlayerService.ActiveRadioChanged -= OnActiveRadioChanged;
        _audioPlayerService.EqSettingsChanged -= OnEqSettingsChanged;
        _audioPlayerService.FadeOutRequested -= OnFadeOutRequested;
        _audioPlayerService.FadeResetRequested -= OnFadeResetRequested;
        _audioPlayerService.CrossfadeRequested -= OnCrossfadeRequested;
        _audioPlayerService.GaplessPrebufferRequested -= OnGaplessPrebufferRequested;
        _audioPlayerService.LoudnessSettingsChanged -= OnLoudnessSettingsChanged;
    }

    private void OnEqSettingsChanged()
    {
        if (_audioPlayerService is null) return;
        _audioEqualizer.UpdateSettings(_audioPlayerService.EqEnabled, _audioPlayerService.EqBands);
    }

    private void OnLoudnessSettingsChanged() => RefreshLoudnessGain(applyToPlayer: true);

    private void RefreshLoudnessGain(bool applyToPlayer = false)
    {
        if (_audioPlayerService is null) return;

        var track = _audioPlayerService.CurrentTrack;
        var linear = LoudnessGainHelper.ComputeLinearGain(
            _audioPlayerService.LoudnessEnabled,
            _audioPlayerService.LoudnessTargetLufs,
            _audioPlayerService.LoudnessPreampDb,
            track?.LoudnessLufs,
            track?.ReplayGainTrackGain);
        _loudnessLinearGain = (float)LoudnessGainHelper.ApplySoftLimiter(linear, _audioPlayerService.LimiterEnabled);

        if (applyToPlayer && !_crossfadeInProgress)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_player is not null)
                    _player.Volume = _loudnessLinearGain;
            });
        }
    }

    private async Task OnGaplessPrebufferRequested(PlayerSource source)
    {
        if (_isVideoMode || _crossfadePlayer is null || string.IsNullOrEmpty(source.Url)) return;

        UpdateAuthHeaders();
        _gaplessPrebufferedUrl = source.Url;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            PreparePlayerWithSource(_crossfadePlayer, source, volume: 0f, playWhenReady: false);
        });
    }

    private async Task OnCrossfadeRequested(PlayerSource source, double durationSeconds)
    {
        if (_isVideoMode || _player is null || _crossfadePlayer is null || string.IsNullOrEmpty(source.Url))
            return;

        _crossfadeInProgress = true;
        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;
        var peak = _loudnessLinearGain;

        try
        {
            UpdateAuthHeaders();

            var alreadyPrepared = string.Equals(_gaplessPrebufferedUrl, source.Url, StringComparison.Ordinal);
            if (!alreadyPrepared)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    PreparePlayerWithSource(_crossfadePlayer, source, volume: 0f, playWhenReady: false));
                await WaitUntilReadyAsync(_crossfadePlayer, ct);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _crossfadePlayer.Volume = 0f;
                _crossfadePlayer.PlayWhenReady = true;
                _crossfadePlayer.Play();
            });

            await EqualPowerCrossfadeAsync(_player, _crossfadePlayer, Math.Max(0.25, durationSeconds), peak, ct);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_crossfadePlayer is not null)
                    _crossfadePlayer.Volume = peak;
                PromoteIncomingInPlace();
            });

            _gaplessPrebufferedUrl = null;
        }
        catch (System.OperationCanceledException)
        {
            // superseded by reset or a newer fade
        }
        finally
        {
            _crossfadeInProgress = false;
            _audioPlayerService?.NotifyCrossfadeCompleted();
        }
    }

    private static async Task WaitUntilReadyAsync(IExoPlayer player, CancellationToken ct)
    {
        // Player.StateReady = 3
        for (var i = 0; i < 100; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (player.PlaybackState == 3)
                return;
            await Task.Delay(50, ct);
        }
    }

    private async Task EqualPowerCrossfadeAsync(
        IExoPlayer outgoing,
        IExoPlayer incoming,
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

    private async Task OnFadeOutRequested(double durationSeconds)
    {
        if (_isVideoMode || _player is null) return;

        _fadeCts?.Cancel();
        _fadeCts?.Dispose();
        _fadeCts = new CancellationTokenSource();
        var ct = _fadeCts.Token;

        try
        {
            await FadePlayerVolumeAsync(_loudnessLinearGain, 0f, Math.Max(0.25, durationSeconds), ct);
        }
        catch (System.OperationCanceledException)
        {
            // superseded by reset or a newer fade
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
            var player = _player;
            if (player is null) return;
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
                _player.Volume = _loudnessLinearGain;
        });
        return Task.CompletedTask;
    }

    private void PreparePlayerWithSource(IExoPlayer player, PlayerSource source, float volume, bool playWhenReady)
    {
        if (string.IsNullOrEmpty(source.Url)) return;

        var uri = source.Url.Contains("://") ? source.Url : $"file://{source.Url}";
        var itemBuilder = new MediaItem.Builder().SetUri(uri)!;

        // Prefer CurrentTrack over deferred _pendingTrack: soft-crossfade advances the
        // queue index before CurrentTrackChanged fires, so _pendingTrack stays one behind.
        var track = _audioPlayerService?.CurrentTrack ?? _pendingTrack;
        if (track is not null)
        {
            itemBuilder.SetMediaId(track.MediaId.ToString())!
                .SetMediaMetadata(BuildTrackMetadata(track)!);
        }

        player.Volume = volume;
        player.SetMediaItem(itemBuilder.Build()!);
        player.Prepare();
        player.PlayWhenReady = playWhenReady;
    }

    private MediaMetadata BuildTrackMetadata(AudioQueueItem track)
    {
        var metadataBuilder = new MediaMetadata.Builder()
            .SetTitle(track.Title)!
            .SetArtist(track.Artist)!
            .SetAlbumTitle(track.AlbumTitle)!
            .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!
            .SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeMusic))!;

        if (track.CoverUrl is not null)
            SetPlayerArtwork(metadataBuilder, track.CoverUrl);

        return metadataBuilder.Build()!;
    }

    private void SyncNowPlayingMetadata(AudioQueueItem? track)
    {
        if (_player is null || track is null || _isVideoMode)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_player is null)
                    return;

                var current = _player.CurrentMediaItem;
                if (current is null)
                    return;

                var index = _player.CurrentMediaItemIndex;
                if (index < 0)
                    return;

                var mediaId = track.MediaId.ToString();
                var currentTitle = current.MediaMetadata?.Title?.ToString();
                if (string.Equals(current.MediaId, mediaId, StringComparison.Ordinal)
                    && string.Equals(currentTitle, track.Title, StringComparison.Ordinal))
                    return;

                var updated = current.BuildUpon()!
                    .SetMediaId(mediaId)!
                    .SetMediaMetadata(BuildTrackMetadata(track))!
                    .Build()!;

                _player.ReplaceMediaItem(index, updated);
                Log.Info(Tag, $"Now-playing metadata synced: {track.Title}");
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Failed to sync now-playing metadata: {ex.Message}");
            }
        });
    }

    private void ApplySingleItemSource(PlayerSource source, float startVolume = 1f)
    {
        if (_player is null || string.IsNullOrEmpty(source.Url)) return;

        _resolvedQueueMediaItems = null;
        _gaplessPrebufferedUrl = null;
        PreparePlayerWithSource(_player, source, startVolume <= 0 ? startVolume : startVolume * _loudnessLinearGain, playWhenReady: true);
        if (startVolume > 0)
            _player.Volume = startVolume * _loudnessLinearGain;
        Log.Info(Tag, $"Playing: {(_audioPlayerService?.CurrentTrack ?? _pendingTrack)?.Title ?? "unknown"}");
    }

    private void OnSourceChanged(PlayerSource source)
    {
        if (_syncingFromExoPlayer) return;
        if (_player is null || string.IsNullOrEmpty(source.Url)) return;
        if (_crossfadeInProgress) return;

        UpdateAuthHeaders();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var uri = source.Url.Contains("://") ? source.Url : $"file://{source.Url}";
                var currentIndex = _audioPlayerService?.CurrentIndex ?? 0;

                // Gapless: promote the prebuffered secondary in place (do not reload).
                if (_crossfadePlayer is not null
                    && string.Equals(_gaplessPrebufferedUrl, source.Url, StringComparison.Ordinal)
                    && _crossfadePlayer.PlaybackState is 2 or 3)
                {
                    var peak = _loudnessLinearGain;
                    _crossfadePlayer.Volume = peak;
                    _crossfadePlayer.PlayWhenReady = true;
                    _crossfadePlayer.Play();
                    PromoteIncomingInPlace();
                    return;
                }

                // Validate resolved queue is still current (not stale from previous session)
                if (_resolvedQueueMediaItems is not null)
                {
                    var currentTrackId = _audioPlayerService?.CurrentTrack?.MediaId.ToString();
                    if (currentIndex < 0 || currentIndex >= _resolvedQueueMediaItems.Count
                        || currentTrackId != _resolvedQueueMediaItems[currentIndex].MediaId)
                    {
                        _resolvedQueueMediaItems = null;
                    }
                }

                // If we have resolved queue items (from OnAddMediaItems), use multi-item playlist
                if (_resolvedQueueMediaItems is not null && _resolvedQueueMediaItems.Count > 1)
                {
                    if (_player.MediaItemCount == _resolvedQueueMediaItems.Count)
                    {
                        _player.SeekToDefaultPosition(currentIndex);
                    }
                    else
                    {
                        _player.SetMediaItems(_resolvedQueueMediaItems, currentIndex, 0L);
                        _player.Prepare();
                    }

                    _player.Volume = _loudnessLinearGain;
                    _player.PlayWhenReady = true;
                    Log.Info(Tag, $"Playing: {_pendingTrack?.Title ?? "unknown"} - URI: {uri[..Math.Min(80, uri.Length)]}");
                }
                else
                {
                    ApplySingleItemSource(source);
                }
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Failed to set media source: {ex}");
            }
        });
    }

    private void PromoteIncomingInPlace()
    {
        if (_player is null || _crossfadePlayer is null || _forwardingPlayer is null)
            return;

        var outgoing = _player;
        var incoming = _crossfadePlayer;

        outgoing.RemoveListener(this);
        incoming.AddListener(this);

        if (_audioAttributes is not null)
        {
            incoming.SetAudioAttributes(_audioAttributes, true);
            outgoing.SetAudioAttributes(_audioAttributes, false);
        }

        _player = incoming;
        _crossfadePlayer = outgoing;
        _forwardingPlayer.SetActivePlayer(incoming);

        outgoing.Stop();
        outgoing.ClearMediaItems();
        outgoing.Volume = 0f;
        _gaplessPrebufferedUrl = null;

        if (incoming.AudioSessionId != AndroidX.Media3.Common.C.AudioSessionIdUnset)
            _audioEqualizer.Attach(incoming.AudioSessionId);

        if (_audioPlayerService is not null)
        {
            _updatingFromPlayer = true;
            _audioPlayerService.CurrentTime = Math.Max(0, incoming.CurrentPosition) / 1000.0;
            if (incoming.Duration > 0)
                _audioPlayerService.Duration = incoming.Duration / 1000.0;
            _audioPlayerService.PlaybackState = incoming.IsPlaying
                ? PlaybackState.Playing
                : PlaybackState.Paused;
            _updatingFromPlayer = false;
        }

        if (incoming.IsPlaying)
            StartPositionUpdates();

        Log.Info(Tag, "Promoted incoming player in place");
        _forwardingPlayer.NotifyQueueChanged();
    }

    private void TransferPlaybackFocusToIncoming()
    {
        if (_audioAttributes is null || _crossfadePlayer is null)
            return;

        _crossfadePlayer.SetAudioAttributes(_audioAttributes, true);
        _player?.SetAudioAttributes(_audioAttributes, false);
    }

    private Task OnPlayRequested()
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _player?.Play();
            if (_crossfadeInProgress)
                _crossfadePlayer?.Play();
        });
        return Task.CompletedTask;
    }

    private Task OnPauseRequested()
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _player?.Pause();
            if (_crossfadeInProgress)
                _crossfadePlayer?.Pause();
        });
        return Task.CompletedTask;
    }

    private Task OnStopRequested()
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _player?.Stop();
            _crossfadePlayer?.Stop();
        });
        return Task.CompletedTask;
    }

    private Task OnSeekRequested(double positionSeconds)
    {
        if (_updatingFromPlayer) return Task.CompletedTask;
        MainThread.BeginInvokeOnMainThread(() =>
            _player?.SeekTo((long)(positionSeconds * 1000)));
        return Task.CompletedTask;
    }

    private AudioQueueItem? _pendingTrack;

    private void OnCurrentTrackChanged(AudioQueueItem? track)
    {
        _pendingTrack = track;
        RefreshLoudnessGain(applyToPlayer: !_crossfadeInProgress);
        // Crossfade defers this event until after promote; push metadata so
        // Android Auto / notification never stay one track behind.
        if (!_crossfadeInProgress && !_syncingFromExoPlayer)
            SyncNowPlayingMetadata(track);
        _forwardingPlayer?.NotifyQueueChanged();
    }

    private void OnActiveRadioChanged()
    {
        if (_audioPlayerService?.ActiveRadioTitle is not null)
            return;

        _radioAwaitingMedia3Playlist = false;
        _radioMediaIdsOnPlayer.Clear();
        CancelRadioPlaylistSyncDebounce();
    }

    private void OnQueueChanged()
    {
        if (_radioAwaitingMedia3Playlist)
            return;
        if (string.IsNullOrEmpty(_audioPlayerService?.ActiveRadioTitle))
            return;

        ScheduleRadioPlaylistSync();
    }

    private void TryCompleteRadioPlaylistHandoff()
    {
        if (!_radioAwaitingMedia3Playlist)
            return;
        if (_player is null || _player.MediaItemCount == 0)
            return;

        _radioAwaitingMedia3Playlist = false;
        ScheduleRadioPlaylistSync();
    }

    private void ScheduleRadioPlaylistSync()
    {
        CancelRadioPlaylistSyncDebounce();
        var cts = new CancellationTokenSource();
        _radioSyncDebounceCts = cts;
        _ = DebouncedRadioPlaylistSyncAsync(cts.Token);
    }

    private void CancelRadioPlaylistSyncDebounce()
    {
        _radioSyncDebounceCts?.Cancel();
        _radioSyncDebounceCts?.Dispose();
        _radioSyncDebounceCts = null;
    }

    private async Task DebouncedRadioPlaylistSyncAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RadioPlaylistSyncDebounce, cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        await SyncRadioPlaylistToPlayerAsync();
    }

    private async Task SyncRadioPlaylistToPlayerAsync()
    {
        if (_audioPlayerService is null || _player is null)
            return;
        if (string.IsNullOrEmpty(_audioPlayerService.ActiveRadioTitle))
            return;
        if (_radioAwaitingMedia3Playlist)
            return;

        try
        {
            await _radioPlaylistSync.WaitAsync();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            while (true)
            {
                if (string.IsNullOrEmpty(_audioPlayerService.ActiveRadioTitle))
                    return;

                var missing = _audioPlayerService.Queue
                    .Where(t => !_radioMediaIdsOnPlayer.Contains(t.MediaId))
                    .ToList();
                if (missing.Count == 0)
                    return;

                var toAdd = new List<MediaItem>();
                foreach (var track in missing)
                {
                    var item = await TryCreatePlayerMediaItemAsync(track);
                    if (item is null)
                        continue;

                    toAdd.Add(item);
                    _radioMediaIdsOnPlayer.Add(track.MediaId);
                }

                if (toAdd.Count == 0)
                    return;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (_player is null)
                        return;

                    _player.AddMediaItems(toAdd);
                    if (_resolvedQueueMediaItems is List<MediaItem> resolved)
                        resolved.AddRange(toAdd);
                    else
                        _resolvedQueueMediaItems = [.. toAdd];

                    _forwardingPlayer?.NotifyQueueChanged();
                    Log.Info(Tag, $"Radio playlist appended {toAdd.Count} track(s), player now has {_player.MediaItemCount}");
                });
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Radio playlist sync failed: {ex.Message}");
        }
        finally
        {
            try
            {
                _radioPlaylistSync.Release();
            }
            catch (ObjectDisposedException)
            {
                // Service is tearing down.
            }
        }
    }

    private bool _acceptEncodingConfigured;

    private void EnsureServerBaseAddress()
    {
        if (_k7ServerService is null || _k7ServerService.HttpClient.BaseAddress is not null)
            return;

        var serverUrl = Preferences.Get(Constants.PreferenceKeys.K7_SERVER_URL, null);
        if (string.IsNullOrEmpty(serverUrl))
            return;

        _k7ServerService.HttpClient.BaseAddress = new Uri(serverUrl);
        Log.Info(Tag, $"BaseAddress set to {serverUrl}");
    }

    private void UpdateAuthHeaders()
    {
        if (_httpDataSourceFactory is null) return;

        EnsureServerBaseAddress();

        // Android Auto service sometimes runs without compression assemblies loaded;
        // ask for identity encoding to avoid gzip/deflate decompression path.
        // Configure once - mutating DefaultRequestHeaders while requests are in
        // flight throws InvalidOperationException. Do this even without a token so
        // unauthenticated/probe requests still decode.
        if (_k7ServerService is not null && !_acceptEncodingConfigured)
        {
            try
            {
                _k7ServerService.HttpClient.DefaultRequestHeaders.AcceptEncoding.Clear();
                _k7ServerService.HttpClient.DefaultRequestHeaders.AcceptEncoding.Add(
                    new StringWithQualityHeaderValue("identity"));
                _acceptEncodingConfigured = true;
            }
            catch (InvalidOperationException ex)
            {
                Log.Warn(Tag, $"Could not set Accept-Encoding: {ex.Message}");
            }
        }

        // Read token directly from device storage (DelegatingHandler adds it per-request to HttpClient,
        // but ExoPlayer's HttpDataSource needs it set explicitly)
        var services = IPlatformApplication.Current?.Services;
        var deviceStorage = services?.GetService<IDeviceStorageService>();
        var token = deviceStorage?.Get(K7.Shared.PreferenceKeys.ACCESS_TOKEN);

        if (!string.IsNullOrEmpty(token))
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}"
            };
            _httpDataSourceFactory.SetDefaultRequestProperties(headers);

            if (_k7ServerService is not null)
            {
                _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }

    private const string LocalFilesPrefix = "https://k7-local-files/";

    private void SetPlayerArtwork(MediaMetadata.Builder metadataBuilder, string coverUrl)
    {
        if (coverUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = coverUrl["file://".Length..];
            if (File.Exists(filePath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(filePath);
                    metadataBuilder.SetArtworkData(bytes, Java.Lang.Integer.ValueOf((int)MediaMetadata.PictureTypeFrontCover));
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Failed to read artwork for player: {ex.Message}");
                }
            }
        }
        else if (coverUrl.StartsWith(LocalFilesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = coverUrl[LocalFilesPrefix.Length..];
            var localPath = Path.Combine(FileSystem.AppDataDirectory, "downloads", relativePath);
            if (File.Exists(localPath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(localPath);
                    metadataBuilder.SetArtworkData(bytes, Java.Lang.Integer.ValueOf((int)MediaMetadata.PictureTypeFrontCover));
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Failed to read artwork for player: {ex.Message}");
                }
            }
        }
        else
        {
            var absoluteUri = _k7ServerService?.GetAbsoluteUri(coverUrl);
            if (absoluteUri is not null)
                metadataBuilder.SetArtworkUri(global::Android.Net.Uri.Parse(absoluteUri.AbsoluteUri));
        }
    }

    // --- Position tracking ---

    private System.Timers.Timer? _positionTimer;

    private void StartPositionUpdates()
    {
        if (_positionTimer is not null) return;

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_player is null || _audioPlayerService is null) return;

                    var position = _player.CurrentPosition / 1000.0;
                    var duration = _player.Duration / 1000.0;
                    var buffered = _player.BufferedPosition / 1000.0;

                    _updatingFromPlayer = true;
                    _audioPlayerService.CurrentTime = position;
                    if (duration > 0)
                        _audioPlayerService.Duration = duration;
                    if (buffered > 0)
                        _audioPlayerService.BufferedTime = buffered;
                    _updatingFromPlayer = false;
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, $"Position update failed: {ex.Message}");
                }
            });
        };
        _positionTimer.Start();
    }

    private void StopPositionUpdates()
    {
        _positionTimer?.Stop();
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    public IListenableFuture? OnGetLibraryRoot(
        MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        LibraryParams? libraryParams)
    {
        var root = new MediaItem.Builder()
            .SetMediaId(RootId)!
            .SetMediaMetadata(new MediaMetadata.Builder()
                .SetIsBrowsable(Java.Lang.Boolean.ValueOf(true))!
                .SetIsPlayable(Java.Lang.Boolean.ValueOf(false))!
                .SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeFolderMixed))!
                .SetTitle("K7")!
                .Build()!)!
            .Build();

        var future = ResolvableFuture.Create()!;
        future.Set(LibraryResult.OfItem(root, libraryParams));
        return future;
    }

    public IListenableFuture? OnGetChildren(
        MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? parentId,
        int page,
        int pageSize,
        LibraryParams? libraryParams)
    {
        return BuildFuture(async () =>
        {
            IReadOnlyList<MediaBrowseItem> items;

            try
            {
                UpdateAuthHeaders();

                if (parentId == RootId)
                    items = await _mediaBrowseService!.GetRootItemsAsync();
                else
                    items = await _mediaBrowseService!.GetChildrenAsync(parentId!);

                Log.Info(Tag, $"OnGetChildren({parentId}): returned {items.Count} items");
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Failed to load children for {parentId}: {ex}");
                items =
                [
                    new MediaBrowseItem
                    {
                        Id = "error:load-failed",
                        Title = "Unable to load",
                        Subtitle = "Check server connection, or use Downloads",
                        IsBrowsable = false,
                        IsPlayable = false
                    }
                ];
            }

            var mediaItems = new List<MediaItem>(items.Count);
            foreach (var browseItem in items)
                mediaItems.Add(await ToMediaItemAsync(browseItem));

            // Grant URI permissions for content:// artwork URIs to the browsing controller
            if (browser?.PackageName is not null)
            {
                foreach (var mi in mediaItems)
                {
                    var artUri = mi.MediaMetadata?.ArtworkUri;
                    if (artUri is not null && artUri.Scheme == "content")
                    {
                        try
                        {
                            GrantUriPermission(browser.PackageName, artUri,
                                global::Android.Content.ActivityFlags.GrantReadUriPermission);
                            Log.Info(Tag, $"Granted URI permission to {browser.PackageName} for {artUri}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(Tag, $"Failed to grant URI permission to {browser.PackageName}: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                Log.Warn(Tag, "Browser package is null, cannot grant URI permissions");
            }

            return LibraryResult.OfItemList(mediaItems, libraryParams)!;
        });
    }

    public IListenableFuture? OnSearch(
        MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? query,
        LibraryParams? libraryParams)
    {
        var future = ResolvableFuture.Create()!;
        future.Set(LibraryResult.OfVoid());
        // Notify that search results are available
        _ = Task.Run(async () =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(query) && browser is not null)
                {
                    var items = await _mediaBrowseService!.SearchAsync(query);
                    Log.Info(Tag, $"OnSearch({query}): found {items.Count} items");
                    session?.NotifySearchResultChanged(browser, query, items.Count, libraryParams);
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Search notification failed: {ex.Message}");
            }
        });
        return future;
    }

    public IListenableFuture? OnGetSearchResult(
        MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? query,
        int page,
        int pageSize,
        LibraryParams? libraryParams)
    {
        return BuildFuture(async () =>
        {
            if (string.IsNullOrWhiteSpace(query))
                return LibraryResult.OfItemList(new List<MediaItem>(), libraryParams)!;

            try
            {
                var items = await _mediaBrowseService!.SearchAsync(query);
                Log.Info(Tag, $"OnGetSearchResult({query}): returned {items.Count} items");
                var mediaItems = new List<MediaItem>(items.Count);
                foreach (var browseItem in items)
                    mediaItems.Add(await ToMediaItemAsync(browseItem));
                return LibraryResult.OfItemList(mediaItems, libraryParams)!;
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"Search failed for '{query}': {ex.Message}");
                return LibraryResult.OfItemList(new List<MediaItem>(), libraryParams)!;
            }
        });
    }

    public IListenableFuture? OnGetItem(
        MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? mediaId)
    {
        var id = mediaId ?? string.Empty;
        var (isBrowsable, isPlayable) = GetItemFlags(id);

        var item = new MediaItem.Builder()
            .SetMediaId(id)!
            .SetMediaMetadata(new MediaMetadata.Builder()
                .SetIsBrowsable(Java.Lang.Boolean.ValueOf(isBrowsable))!
                .SetIsPlayable(Java.Lang.Boolean.ValueOf(isPlayable))!
                .Build()!)!
            .Build();

        var future = ResolvableFuture.Create()!;
        future.Set(LibraryResult.OfItem(item, null));
        return future;
    }

    private static (bool IsBrowsable, bool IsPlayable) GetItemFlags(string mediaId)
    {
        if (mediaId.StartsWith("root:"))
            return (true, false);

        if (mediaId.StartsWith("home:section:", StringComparison.Ordinal)
            || mediaId.StartsWith("home-", StringComparison.Ordinal))
            return (true, false);

        // Any other home:* IDs are non-actionable placeholders.
        if (mediaId.StartsWith("home:", StringComparison.Ordinal))
            return (false, false);

        if (string.IsNullOrWhiteSpace(mediaId))
            return (false, false);

        if (mediaId.StartsWith("albums-letter:") || mediaId.StartsWith("artists-letter:"))
            return (true, false);

        if (mediaId.StartsWith("artist:"))
        {
            // "artist:guid:shuffle" = playable, "artist:guid" = browsable
            return mediaId.EndsWith(":shuffle") ? (false, true) : (true, false);
        }

        if (mediaId.StartsWith("album:"))
        {
            // "album:guid" = browsable album, "album:guid:shuffle" = playable shuffle, "album:guid:trackId" = playable track
            var afterPrefix = mediaId.AsSpan("album:".Length);
            return afterPrefix.Contains(':') ? (false, true) : (true, true);
        }

        if (mediaId.StartsWith("playlist:"))
        {
            var afterPrefix = mediaId.AsSpan("playlist:".Length);
            return afterPrefix.Contains(':') ? (false, true) : (true, true);
        }

        if (mediaId.StartsWith("radio:"))
            return (false, true);

        if (mediaId.StartsWith("download-group:"))
        {
            var afterPrefix = mediaId.AsSpan("download-group:".Length);
            return afterPrefix.Contains(':') ? (false, true) : (true, true);
        }

        return (false, true);
    }

    public IListenableFuture? OnAddMediaItems(
        MediaSession? session,
        MediaSession.ControllerInfo? controller,
        IList<MediaItem>? mediaItems)
    {
        if (mediaItems is null || mediaItems.Count == 0)
        {
            Log.Warn(Tag, "OnAddMediaItems: empty input");
            var empty = ResolvableFuture.Create()!;
            empty.Set(new Java.Util.ArrayList());
            return empty;
        }

        Log.Info(Tag, $"OnAddMediaItems: resolving {mediaItems.Count} item(s), mediaId={mediaItems[0]?.MediaId}");

        return BuildFuture(async () =>
        {
            var resolvedItems = new Java.Util.ArrayList();

            try
            {
                foreach (var item in mediaItems)
                {
                    var mediaId = item.MediaId;
                    if (string.IsNullOrEmpty(mediaId)) continue;

                    var isRadio = mediaId.StartsWith("radio:", StringComparison.Ordinal);

                    // Block OnSourceChanged while resolving so Media3 owns the first SetMediaItem.
                    _syncingFromExoPlayer = true;
                    var queueItems = await _mediaBrowseService!.GetPlayableItemsAsync(mediaId);
                    if (isRadio && _audioPlayerService is { Queue.Count: > 0 })
                        queueItems = _audioPlayerService.Queue.ToArray();

                    Log.Info(Tag, $"OnAddMediaItems: GetPlayableItemsAsync({mediaId}) returned {queueItems.Count} tracks");

                    if (queueItems.Count > 0)
                    {
                        var failCount = 0;
                        var resolvedList = new List<MediaItem>();
                        var resolvedIds = new HashSet<Guid>();

                        async Task ResolveTracksAsync(IReadOnlyList<AudioQueueItem> tracks)
                        {
                            foreach (var track in tracks)
                            {
                                if (!resolvedIds.Add(track.MediaId))
                                    continue;

                                var resolved = await TryCreatePlayerMediaItemAsync(track);
                                if (resolved is null)
                                {
                                    resolvedIds.Remove(track.MediaId);
                                    failCount++;
                                    continue;
                                }

                                resolvedList.Add(resolved);
                                resolvedItems.Add(resolved);
                            }
                        }

                        await ResolveTracksAsync(queueItems);
                        if (isRadio && _audioPlayerService is not null
                            && _audioPlayerService.Queue.Count > resolvedList.Count)
                            await ResolveTracksAsync(_audioPlayerService.Queue.ToArray());

                        if (failCount > 0)
                            Log.Warn(Tag, $"OnAddMediaItems: {failCount} tracks failed to get stream URL");

                        _resolvedQueueMediaItems = resolvedList;

                        if (isRadio)
                        {
                            _radioMediaIdsOnPlayer.Clear();
                            foreach (var id in resolvedIds)
                                _radioMediaIdsOnPlayer.Add(id);
                            _radioAwaitingMedia3Playlist = true;
                            _pendingTrack = _audioPlayerService?.CurrentTrack ?? queueItems[0];
                            _syncingFromExoPlayer = false;
                        }
                        else
                        {
                            _radioAwaitingMedia3Playlist = false;
                            _radioMediaIdsOnPlayer.Clear();

                            try
                            {
                                await MainThread.InvokeOnMainThreadAsync(() => _audioPlayerService!.PlayTracksAsync(queueItems, 0));
                            }
                            finally
                            {
                                _syncingFromExoPlayer = false;
                            }
                        }

                        Log.Info(Tag, $"OnAddMediaItems: queue ready with {resolvedItems.Size()} items for: {mediaId}");
                    }
                    else
                    {
                        // Never hand Media3 an unresolved item (no URI) - ExoPlayer NPEs in
                        // DefaultMediaSourceFactory.createMediaSource when Uri is null.
                        _syncingFromExoPlayer = false;
                        Log.Warn(Tag, $"OnAddMediaItems: no playable tracks for {mediaId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _syncingFromExoPlayer = false;
                Log.Error(Tag, $"OnAddMediaItems: exception: {ex}");
            }

            // Ensure auth headers are set before Media3 starts playback
            UpdateAuthHeaders();

            Log.Info(Tag, $"OnAddMediaItems: returning {resolvedItems.Size()} resolved items to Media3");
            return (Java.Lang.Object)resolvedItems;
        });
    }

    private async Task<MediaItem?> TryCreatePlayerMediaItemAsync(AudioQueueItem track)
    {
        var streamUrl = await GetStreamUrl(track);
        if (streamUrl is null)
            return null;

        return new MediaItem.Builder()
            .SetMediaId(track.MediaId.ToString())!
            .SetUri(streamUrl)!
            .SetMediaMetadata(BuildTrackMetadata(track))!
            .Build()!;
    }

    private async Task<string?> GetStreamUrl(AudioQueueItem track)
    {
        if (!string.IsNullOrEmpty(track.LocalPath))
            return track.LocalPath.Contains("://") ? track.LocalPath : $"file://{track.LocalPath}";

        if (_streamUriService is null) return null;

        var streamSession = await _streamUriService.GetOrCreateSessionAsync(track.IndexedFileId);
        return streamSession.Source?.Uri.AbsoluteUri;
    }

    private async Task<MediaItem> ToMediaItemAsync(MediaBrowseItem item)
    {
        var metadataBuilder = new MediaMetadata.Builder()
            .SetTitle(item.Title)!
            .SetIsBrowsable(Java.Lang.Boolean.ValueOf(item.IsBrowsable))!
            .SetIsPlayable(Java.Lang.Boolean.ValueOf(item.IsPlayable))!;

        if (item.Subtitle is not null)
            metadataBuilder.SetArtist(item.Subtitle);

        if (item.ArtworkUrl is not null)
        {
            if (item.ArtworkUrl.StartsWith("file://", StringComparison.Ordinal))
            {
                // For local files, embed bitmap data directly (Android Auto can't load content:// reliably)
                var filePath = item.ArtworkUrl["file://".Length..];
                if (File.Exists(filePath))
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(filePath);
                        metadataBuilder.SetArtworkData(bytes, Java.Lang.Integer.ValueOf((int)MediaMetadata.PictureTypeFrontCover));
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"Failed to read artwork file: {ex.Message}");
                    }
                }
                else
                {
                    Log.Warn(Tag, $"Artwork file not found: {filePath}");
                }
            }
            else
            {
                var artworkSet = false;

                // Prefer embedding bytes fetched with authenticated HttpClient; Android Auto host cannot always fetch protected URLs.
                if (_k7ServerService is not null)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        var artworkBytes = await _k7ServerService.HttpClient.GetByteArrayAsync(item.ArtworkUrl, cts.Token);
                        if (artworkBytes.Length > 0)
                        {
                            metadataBuilder.SetArtworkData(artworkBytes, Java.Lang.Integer.ValueOf((int)MediaMetadata.PictureTypeFrontCover));
                            artworkSet = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn(Tag, $"Failed to download browse artwork: {ex.Message}");
                    }
                }

                if (!artworkSet)
                {
                    var artworkUri = ResolveArtworkUriForBrowse(item.ArtworkUrl);
                    if (artworkUri is not null)
                        metadataBuilder.SetArtworkUri(artworkUri);
                    else
                        Log.Warn(Tag, $"Artwork resolved to null for: {item.ArtworkUrl}");
                }
            }
        }

        if (item.IsBrowsable && !item.IsPlayable)
            metadataBuilder.SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeFolderMixed));

        return new MediaItem.Builder()
            .SetMediaId(item.Id)!
            .SetMediaMetadata(metadataBuilder.Build()!)!
            .Build()!;
    }

    private static global::Android.Net.Uri? ResolveArtworkUriForBrowse(string artworkUrl)
    {
        if (artworkUrl.StartsWith("file://", StringComparison.Ordinal))
        {
            var filePath = artworkUrl["file://".Length..];
            var file = new Java.IO.File(filePath);
            if (!file.Exists())
            {
                Log.Warn(Tag, $"Artwork file not found: {filePath}");
                return null;
            }

            try
            {
                return AndroidX.Core.Content.FileProvider.GetUriForFile(
                    global::Android.App.Application.Context,
                    "com.k7.maui.fileprovider",
                    file);
            }
            catch (Exception ex)
            {
                Log.Warn(Tag, $"FileProvider failed for {filePath}: {ex.Message}");
                return null;
            }
        }

        return global::Android.Net.Uri.Parse(artworkUrl);
    }

    private static IListenableFuture BuildFuture<T>(Func<Task<T>> asyncFunc)
        where T : Java.Lang.Object
    {
        var future = ResolvableFuture.Create()!;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await asyncFunc();
                future.Set(result);
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Error in media library callback: {ex.Message}");
                future.SetException(new Java.Lang.RuntimeException(ex.Message));
            }
        });

        return future;
    }

    // --- Video mode: PlayerService events -> notification ---

    private void SubscribeToVideoPlayerEvents()
    {
        if (_playerService is null) return;

        _playerService.IsVisibleChanged += OnVideoVisibilityChanged;
        _playerService.PlaybackStateChanged += OnVideoPlaybackStateChanged;
        _playerService.SourceChanged += OnVideoSourceChanged;
    }

    private void UnsubscribeFromVideoPlayerEvents()
    {
        if (_playerService is null) return;

        _playerService.IsVisibleChanged -= OnVideoVisibilityChanged;
        _playerService.PlaybackStateChanged -= OnVideoPlaybackStateChanged;
        _playerService.SourceChanged -= OnVideoSourceChanged;
    }

    private void OnVideoVisibilityChanged()
    {
        if (_playerService is null || _session is null) return;

        if (_playerService.IsVisible)
        {
            EnterVideoMode();
        }
        else
        {
            ExitVideoMode();
        }
    }

    private void EnterVideoMode()
    {
        _isVideoMode = true;
        _audioEqualizer.Detach();
        Log.Info(Tag, "Entering video mode - activating video session");

        var source = _playerService?.Source;
        _videoSessionPlayer?.Activate(source?.Title, source?.CoverUrl);

        if (_videoSession is not null && !_videoSessionAdded)
        {
            AddSession(_videoSession);
            _videoSessionAdded = true;
        }
    }

    private void ExitVideoMode()
    {
        if (!_isVideoMode) return;

        _isVideoMode = false;
        Log.Info(Tag, "Exiting video mode - removing video session");

        _videoSessionPlayer?.Deactivate();

        if (_videoSession is not null && _videoSessionAdded)
        {
            RemoveSession(_videoSession);
            _videoSessionAdded = false;
        }

        if (_player is not null && _player.AudioSessionId != AndroidX.Media3.Common.C.AudioSessionIdUnset)
            _audioEqualizer.Attach(_player.AudioSessionId);
    }

    private void OnVideoPlaybackStateChanged(PlaybackState state)
    {
        if (!_isVideoMode) return;

        if (state is PlaybackState.Ended or PlaybackState.Idle && _playerService?.IsVisible == false)
        {
            ExitVideoMode();
            return;
        }

        _videoSessionPlayer?.NotifyStateChanged();
    }

    private void OnVideoSourceChanged(PlayerSource source)
    {
        if (!_isVideoMode) return;

        _videoSessionPlayer?.UpdateMetadata(source.Title, source.CoverUrl);
    }
}
