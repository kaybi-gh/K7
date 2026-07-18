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
using System.Net.Http.Headers;
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

    private volatile bool _updatingFromPlayer;
    private volatile bool _isVideoMode;
    private bool _videoSessionAdded;
    private bool _syncingFromExoPlayer;
    private IList<MediaItem>? _resolvedQueueMediaItems;

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
        if (_k7ServerService.HttpClient.BaseAddress is null)
        {
            var serverUrl = Preferences.Get(Constants.PreferenceKeys.K7_SERVER_URL, null);
            if (!string.IsNullOrEmpty(serverUrl))
            {
                _k7ServerService.HttpClient.BaseAddress = new Uri(serverUrl);
                Log.Info(Tag, $"BaseAddress set to {serverUrl}");
            }
        }

        _httpDataSourceFactory = new DefaultHttpDataSource.Factory();
        UpdateAuthHeaders();

        var dataSourceFactory = new DefaultDataSource.Factory(this, _httpDataSourceFactory);
        var mediaSourceFactory = new DefaultMediaSourceFactory(this as Context);
        mediaSourceFactory.SetDataSourceFactory(dataSourceFactory);

        var audioAttributes = new AudioAttributes.Builder()!
            .SetUsage((int)global::Android.Media.AudioUsageKind.Media)!
            .SetContentType((int)global::Android.Media.AudioContentType.Music)!
            .Build()!;

#pragma warning disable CS0618 // IMediaSourceFactory marked obsolete in .NET Android bindings but is the correct Media3 API
        _player = new ExoPlayerBuilder(this)!
            .SetMediaSourceFactory(mediaSourceFactory as AndroidX.Media3.ExoPlayer.Source.IMediaSourceFactory)!
            .SetAudioAttributes(audioAttributes, /* handleAudioFocus */ true)!
            .SetHandleAudioBecomingNoisy(true)!
            .SetWakeMode(AndroidX.Media3.Common.C.WakeModeLocal)!
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

        try
        {
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
        if (reason == 1 && _resolvedQueueMediaItems is not null && _resolvedQueueMediaItems.Count > 1)
        {
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
        if (_syncingFromExoPlayer) return;
        if (_player is null || string.IsNullOrEmpty(source.Url)) return;

        UpdateAuthHeaders();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var uri = source.Url.Contains("://") ? source.Url : $"file://{source.Url}";
                var currentIndex = _audioPlayerService?.CurrentIndex ?? 0;

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
                        // Queue already loaded on ExoPlayer - just seek to new track (next/prev)
                        _player.SeekToDefaultPosition(currentIndex);
                    }
                    else
                    {
                        // First time loading this queue
                        _player.SetMediaItems(_resolvedQueueMediaItems, currentIndex, 0L);
                        _player.Prepare();
                    }
                }
                else
                {
                    // Fallback: single item (Blazor UI or no resolved queue)
                    _resolvedQueueMediaItems = null;
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
                            SetPlayerArtwork(metadataBuilder, _pendingTrack.CoverUrl);

                        itemBuilder.SetMediaId(_pendingTrack.MediaId.ToString())!
                            .SetMediaMetadata(metadataBuilder.Build()!);
                    }

                    _player.SetMediaItem(itemBuilder.Build()!);
                    _player.Prepare();
                }

                _player.PlayWhenReady = true;

                Log.Info(Tag, $"Playing: {_pendingTrack?.Title ?? "unknown"} - URI: {uri[..Math.Min(80, uri.Length)]}");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Failed to set media source: {ex}");
            }
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
        if (_httpDataSourceFactory is null) return;

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

                // Android Auto service sometimes runs without compression assemblies loaded;
                // ask for identity encoding to avoid gzip/deflate decompression path.
                _k7ServerService.HttpClient.DefaultRequestHeaders.AcceptEncoding.Clear();
                _k7ServerService.HttpClient.DefaultRequestHeaders.AcceptEncoding.Add(
                    new StringWithQualityHeaderValue("identity"));
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
                items = [];
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

                    var queueItems = await _mediaBrowseService!.GetPlayableItemsAsync(mediaId);
                    Log.Info(Tag, $"OnAddMediaItems: GetPlayableItemsAsync({mediaId}) returned {queueItems.Count} tracks");

                    if (queueItems.Count > 0)
                    {
                        var failCount = 0;
                        foreach (var track in queueItems)
                        {
                            var streamUrl = await GetStreamUrl(track);
                            if (streamUrl is null)
                            {
                                failCount++;
                                continue;
                            }

                            var metadataBuilder = new MediaMetadata.Builder()
                                .SetTitle(track.Title)!
                                .SetArtist(track.Artist)!
                                .SetAlbumTitle(track.AlbumTitle)!
                                .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!
                                .SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeMusic))!;

                            if (track.CoverUrl is not null)
                                SetPlayerArtwork(metadataBuilder, track.CoverUrl);

                            var resolved = new MediaItem.Builder()
                                .SetMediaId(track.MediaId.ToString())!
                                .SetUri(streamUrl)!
                                .SetMediaMetadata(metadataBuilder.Build()!)!
                                .Build()!;

                            resolvedItems.Add(resolved);
                        }

                        if (failCount > 0)
                            Log.Warn(Tag, $"OnAddMediaItems: {failCount} tracks failed to get stream URL");

                        // Store resolved items for queue navigation
                        var resolvedList = new List<MediaItem>();
                        for (var i = 0; i < resolvedItems.Size(); i++)
                            resolvedList.Add((MediaItem)resolvedItems.Get(i)!);
                        _resolvedQueueMediaItems = resolvedList;

                        // Block OnSourceChanged - Media3 will handle setting items on the player
                        _syncingFromExoPlayer = true;
                        try
                        {
                            await MainThread.InvokeOnMainThreadAsync(() => _audioPlayerService!.PlayTracksAsync(queueItems, 0));
                        }
                        finally
                        {
                            _syncingFromExoPlayer = false;
                        }

                        Log.Info(Tag, $"OnAddMediaItems: queue ready with {resolvedItems.Size()} items for: {mediaId}");
                    }
                    else if (item is not null)
                    {
                        resolvedItems.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"OnAddMediaItems: exception: {ex}");
            }

            // Ensure auth headers are set before Media3 starts playback
            UpdateAuthHeaders();

            Log.Info(Tag, $"OnAddMediaItems: returning {resolvedItems.Size()} resolved items to Media3");
            return (Java.Lang.Object)resolvedItems;
        });
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
                        var artworkBytes = await _k7ServerService.HttpClient.GetByteArrayAsync(item.ArtworkUrl);
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
