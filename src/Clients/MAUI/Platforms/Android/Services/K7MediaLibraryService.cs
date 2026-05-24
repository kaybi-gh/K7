using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Interfaces;
using Log = Android.Util.Log;

#pragma warning disable XAOBS001 // ResolvableFuture is the only way to create IListenableFuture in .NET Android bindings

namespace K7.Clients.MAUI.Platforms.Android.Services;

[Service(
    Name = "com.k7.maui.K7MediaLibraryService",
    ForegroundServiceType = ForegroundService.TypeMediaPlayback,
    Exported = true)]
[IntentFilter(["androidx.media3.session.MediaLibraryService"])]
public class K7MediaLibraryService : MediaLibraryService, MediaLibraryService.MediaLibrarySession.ICallback
{
    private const string Tag = "K7-MediaLibrary";
    private const string RootId = "k7_root";

    private IExoPlayer? _player;
    private MediaLibrarySession? _session;

    private IMediaBrowseService? _mediaBrowseService;
    private IAudioPlayerService? _audioPlayerService;

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

        _player = new ExoPlayerBuilder(this)!
            .Build()!;

        _session = new MediaLibrarySession.Builder(this, _player, this)!
            .Build()!;
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
        _session?.Release();
        _player?.Release();
        _session = null;
        _player = null;
        base.OnDestroy();
        Log.Info(Tag, "K7MediaLibraryService destroyed");
    }

    public override IBinder? OnBind(Intent? intent)
    {
        return base.OnBind(intent);
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

    private static async Task<string?> GetStreamUrl(AudioQueueItem track)
    {
        var streamService = IPlatformApplication.Current?.Services?.GetService<IStreamUriService>();
        if (streamService is null) return null;

        var streamSession = await streamService.GetOrCreateSessionAsync(track.IndexedFileId);
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
