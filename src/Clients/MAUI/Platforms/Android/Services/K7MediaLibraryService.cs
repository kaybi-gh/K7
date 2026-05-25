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

    private bool _updatingFromPlayer;
    private bool _isVideoMode;
    private bool _videoSessionAdded;

    public override void OnCreate()
    {
        base.OnCreate();
        Log.Info(Tag, "K7MediaLibraryService created");

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

        _httpDataSourceFactory = new DefaultHttpDataSource.Factory();
        UpdateAuthHeaders();

        var dataSourceFactory = new DefaultDataSource.Factory(this, _httpDataSourceFactory);
        var mediaSourceFactory = new DefaultMediaSourceFactory(this as Context);
        mediaSourceFactory.SetDataSourceFactory(dataSourceFactory);

#pragma warning disable CS0618 // IMediaSourceFactory marked obsolete in .NET Android bindings but is the correct Media3 API
        _player = new ExoPlayerBuilder(this)!
            .SetMediaSourceFactory(mediaSourceFactory as AndroidX.Media3.ExoPlayer.Source.IMediaSourceFactory)!
            .Build()!;
#pragma warning restore CS0618

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

        _session = new MediaLibrarySession.Builder(this, _forwardingPlayer, this)!
            .SetSessionActivity(pendingIntent)!
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
        _videoSession?.Release();
        _session?.Release();
        _player?.RemoveListener(this);
        _player?.Release();
        _forwardingPlayer = null;
        _videoSession = null;
        _session = null;
        _player = null;
        base.OnDestroy();
        Log.Info(Tag, "K7MediaLibraryService destroyed");
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

        _updatingFromPlayer = true;
        // Player state constants: Idle=1, Buffering=2, Ready=3, Ended=4
        _audioPlayerService.PlaybackState = playbackState switch
        {
            3 => _player?.IsPlaying == true
                ? PlaybackState.Playing
                : PlaybackState.Paused,
            2 => PlaybackState.Buffering,
            4 => PlaybackState.Ended,
            _ => PlaybackState.Idle,
        };
        _updatingFromPlayer = false;
    }

    public void OnIsPlayingChanged(bool isPlaying)
    {
        if (_isVideoMode) return; // Video mode handled by K7VideoSessionPlayer
        if (_audioPlayerService is null) return;

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

    public void OnPlayerError(PlaybackException? error)
    {
        Log.Error(Tag, $"ExoPlayer error: {error?.Message} (code={error?.ErrorCode})");
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
    }

    private void OnSourceChanged(PlayerSource source)
    {
        if (_player is null || string.IsNullOrEmpty(source.Url)) return;

        UpdateAuthHeaders();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var uri = source.Url.Contains("://") ? source.Url : $"file://{source.Url}";

            var itemBuilder = new MediaItem.Builder()
                .SetUri(uri)!;

            if (_pendingTrack is not null)
            {
                var metadataBuilder = new MediaMetadata.Builder()
                    .SetTitle(_pendingTrack.Title)!
                    .SetArtist(_pendingTrack.Artist)!
                    .SetAlbumTitle(_pendingTrack.AlbumTitle)!
                    .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!
                    .SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeMusic))!;

                if (_pendingTrack.CoverUrl is not null)
                {
                    var artworkUri = ResolveArtworkUri(_pendingTrack.CoverUrl);
                    if (artworkUri is not null)
                        metadataBuilder.SetArtworkUri(artworkUri);
                }

                itemBuilder.SetMediaId(_pendingTrack.MediaId.ToString())!
                    .SetMediaMetadata(metadataBuilder.Build()!);
            }

            var mediaItem = itemBuilder.Build()!;
            _player.SetMediaItem(mediaItem);
            _player.Prepare();
            _player.PlayWhenReady = true;

            Log.Info(Tag, $"Playing: {_pendingTrack?.Title ?? "unknown"} - URI: {uri[..Math.Min(80, uri.Length)]}");
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
        MainThread.BeginInvokeOnMainThread(() => _player?.Stop());
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
    }

    private void UpdateAuthHeaders()
    {
        var authHeader = _k7ServerService?.HttpClient.DefaultRequestHeaders.Authorization;
        if (authHeader is not null && _httpDataSourceFactory is not null)
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = authHeader.ToString()
            };
            _httpDataSourceFactory.SetDefaultRequestProperties(headers);
        }
    }

    private const string LocalFilesPrefix = "https://k7-local-files/";

    private global::Android.Net.Uri? ResolveArtworkUri(string coverUrl)
    {
        if (coverUrl.StartsWith(LocalFilesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = coverUrl[LocalFilesPrefix.Length..];
            var localPath = Path.Combine(FileSystem.AppDataDirectory, "downloads", relativePath);
            if (File.Exists(localPath))
                return global::Android.Net.Uri.Parse($"file://{localPath}");
            return null;
        }

        var absoluteUri = _k7ServerService?.GetAbsoluteUri(coverUrl);
        return absoluteUri is not null
            ? global::Android.Net.Uri.Parse(absoluteUri.AbsoluteUri)
            : null;
    }

    // --- Position tracking ---

    private System.Timers.Timer? _positionTimer;

    private void StartPositionUpdates()
    {
        if (_positionTimer is not null) return;

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += (_, _) =>
        {
            // ExoPlayer must be accessed from the main thread
            MainThread.BeginInvokeOnMainThread(() =>
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

            if (parentId == RootId)
                items = await _mediaBrowseService!.GetRootItemsAsync();
            else
                items = await _mediaBrowseService!.GetChildrenAsync(parentId!);

            var mediaItems = items.Select(ToMediaItem).ToList();
            return LibraryResult.OfItemList(mediaItems, libraryParams)!;
        });
    }

    public IListenableFuture? OnGetItem(
        MediaLibrarySession? session,
        MediaSession.ControllerInfo? browser,
        string? mediaId)
    {
        var item = new MediaItem.Builder()
            .SetMediaId(mediaId ?? string.Empty)!
            .Build();

        var future = ResolvableFuture.Create()!;
        future.Set(LibraryResult.OfItem(item, null));
        return future;
    }

    public IListenableFuture? OnAddMediaItems(
        MediaSession? session,
        MediaSession.ControllerInfo? controller,
        IList<MediaItem>? mediaItems)
    {
        if (mediaItems is null || mediaItems.Count == 0)
        {
            var empty = ResolvableFuture.Create()!;
            empty.Set(new MediaSession.MediaItemsWithStartPosition(
                new List<MediaItem>(), 0, 0L));
            return empty;
        }

        return BuildFuture(async () =>
        {
            var resolvedItems = new List<MediaItem>();

            foreach (var item in mediaItems)
            {
                var mediaId = item.MediaId;
                if (string.IsNullOrEmpty(mediaId)) continue;

                var queueItems = await _mediaBrowseService!.GetPlayableItemsAsync(mediaId);

                if (queueItems.Count > 0)
                {
                    await _audioPlayerService!.PlayTracksAsync(queueItems, 0);

                    foreach (var track in queueItems)
                    {
                        var streamUrl = await GetStreamUrl(track);
                        if (streamUrl is null) continue;

                        var resolved = new MediaItem.Builder()
                            .SetMediaId(track.MediaId.ToString())!
                            .SetUri(streamUrl)!
                            .SetMediaMetadata(new MediaMetadata.Builder()
                                .SetTitle(track.Title)!
                                .SetArtist(track.Artist)!
                                .SetAlbumTitle(track.AlbumTitle)!
                                .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!
                                .Build()!)!
                            .Build()!;

                        resolvedItems.Add(resolved);
                    }
                }
                else if (item is not null)
                {
                    resolvedItems.Add(item);
                }
            }

            return (Java.Lang.Object)new MediaSession.MediaItemsWithStartPosition(
                resolvedItems, 0, 0L);
        });
    }

    private async Task<string?> GetStreamUrl(AudioQueueItem track)
    {
        if (_streamUriService is null) return null;

        var streamSession = await _streamUriService.GetOrCreateSessionAsync(track.IndexedFileId);
        return streamSession.Source?.Uri.AbsoluteUri;
    }

    private static MediaItem ToMediaItem(MediaBrowseItem item)
    {
        var metadataBuilder = new MediaMetadata.Builder()
            .SetTitle(item.Title)!
            .SetIsBrowsable(Java.Lang.Boolean.ValueOf(item.IsBrowsable))!
            .SetIsPlayable(Java.Lang.Boolean.ValueOf(item.IsPlayable))!;

        if (item.Subtitle is not null)
            metadataBuilder.SetArtist(item.Subtitle);

        if (item.ArtworkUrl is not null)
            metadataBuilder.SetArtworkUri(global::Android.Net.Uri.Parse(item.ArtworkUrl));

        if (item.IsBrowsable && !item.IsPlayable)
            metadataBuilder.SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeFolderMixed));

        return new MediaItem.Builder()
            .SetMediaId(item.Id)!
            .SetMediaMetadata(metadataBuilder.Build()!)!
            .Build()!;
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
