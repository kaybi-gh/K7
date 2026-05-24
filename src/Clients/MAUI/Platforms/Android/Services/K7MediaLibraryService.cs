using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using Log = Android.Util.Log;

#pragma warning disable XAOBS001 // ResolvableFuture is the only way to create IListenableFuture in .NET Android bindings

namespace K7.Clients.MAUI.Platforms.Android.Services;

[Service(
    Name = "com.k7.maui.K7MediaLibraryService",
    ForegroundServiceType = ForegroundService.TypeMediaPlayback,
    Exported = true)]
[IntentFilter(["androidx.media3.session.MediaLibraryService"])]
public class K7MediaLibraryService : MediaLibraryService,
    MediaLibraryService.MediaLibrarySession.ICallback,
    IPlayerListener
{
    private const string Tag = "K7-MediaLibrary";
    private const string RootId = "k7_root";

    private IExoPlayer? _player;
    private K7ForwardingPlayer? _forwardingPlayer;
    private MediaLibrarySession? _session;

    private IMediaBrowseService? _mediaBrowseService;
    private IAudioPlayerService? _audioPlayerService;
    private IStreamUriService? _streamUriService;

    private bool _updatingFromPlayer;

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
        _streamUriService = services.GetRequiredService<IStreamUriService>();

        _player = new ExoPlayerBuilder(this)!
            .Build()!;

        _player.AddListener(this);

        _forwardingPlayer = new K7ForwardingPlayer(
            _player,
            hasNext: () => _audioPlayerService.CurrentIndex < _audioPlayerService.Queue.Count - 1,
            hasPrevious: () => _audioPlayerService.CurrentIndex > 0 || _audioPlayerService.CurrentTime > 3,
            onSeekToNext: () => _ = _audioPlayerService.NextAsync(),
            onSeekToPrevious: () => _ = _audioPlayerService.PreviousAsync());

        _session = new MediaLibrarySession.Builder(this, _forwardingPlayer, this)!
            .Build()!;

        SubscribeToAudioPlayerEvents();
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
        _session?.Release();
        _player?.RemoveListener(this);
        _player?.Release();
        _forwardingPlayer = null;
        _session = null;
        _player = null;
        base.OnDestroy();
        Log.Info(Tag, "K7MediaLibraryService destroyed");
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return base.OnBind(intent);
    }

    // --- IPlayerListener: ExoPlayer state -> IAudioPlayerService ---

    public void OnPlaybackStateChanged(int playbackState)
    {
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

    public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
    {
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

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var builder = MediaItem.FromUri(source.Url)!.BuildUpon()!;

            if (_pendingTrack is not null)
            {
                var metadataBuilder = new MediaMetadata.Builder()
                    .SetTitle(_pendingTrack.Title)!
                    .SetArtist(_pendingTrack.Artist)!
                    .SetAlbumTitle(_pendingTrack.AlbumTitle)!
                    .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!;

                if (_pendingTrack.CoverUrl is not null)
                {
                    var absoluteUri = IPlatformApplication.Current?.Services?
                        .GetService<IK7ServerService>()?.GetAbsoluteUri(_pendingTrack.CoverUrl);
                    if (absoluteUri is not null)
                        metadataBuilder.SetArtworkUri(global::Android.Net.Uri.Parse(absoluteUri.AbsoluteUri));
                }

                builder.SetMediaId(_pendingTrack.MediaId.ToString())!
                    .SetMediaMetadata(metadataBuilder.Build()!);
            }

            _player.SetMediaItem(builder.Build()!);
            _player.Prepare();
            _player.PlayWhenReady = true;
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

    // --- Position tracking ---

    private System.Timers.Timer? _positionTimer;

    private void StartPositionUpdates()
    {
        if (_positionTimer is not null) return;

        _positionTimer = new System.Timers.Timer(500);
        _positionTimer.Elapsed += (_, _) =>
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
}
