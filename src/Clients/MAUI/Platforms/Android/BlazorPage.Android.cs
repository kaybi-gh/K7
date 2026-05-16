using AndroidX.Media3.Common;
using CommunityToolkit.Maui.Views;
using K7.Clients.Shared.Models;
using Log = Android.Util.Log;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private const string Tag = "K7-Player";
    private AudioNavListener? _audioNavListener;
    private bool _isHandlingNavTransition;

    partial void InitializePlayerPlatform()
    {
        _playerService.SwitchAudioTrackRequested += OnSwitchAudioTrack;
        _playerService.SwitchSubtitleTrackRequested += OnSwitchSubtitleTrack;
        _audioPlayerService.CurrentTrackChanged += OnAudioCurrentTrackChangedAndroid;
    }

    private void OnAudioCurrentTrackChangedAndroid(AudioQueueItem? track)
    {
        if (track is null) return;

        // Wait for the toolkit to set the source, then inject nav placeholders
        _ = Task.Run(async () =>
        {
            await Task.Delay(300);
            MainThread.BeginInvokeOnMainThread(SetupAudioNavigation);
        });
    }

    private void SetupAudioNavigation()
    {
        var player = GetPlayer(NativeAudioPlayer);
        if (player is null)
        {
            Log.Warn(Tag, "Cannot set up audio navigation: player is null");
            return;
        }

        // Remove previous listener
        if (_audioNavListener is not null)
        {
            player.RemoveListener(_audioNavListener);
            _audioNavListener.Dispose();
        }

        // The toolkit sets a single MediaItem. Add placeholders before/after
        // to make COMMAND_SEEK_TO_PREVIOUS and COMMAND_SEEK_TO_NEXT available.
        var itemCount = player.MediaItemCount;
        if (itemCount == 0) return;

        _isHandlingNavTransition = false;

        var placeholder = new MediaItem.Builder()
            .SetMediaId("k7-nav-placeholder")!
            .SetUri("k7://nav-placeholder")!
            .Build();

        // Insert placeholder before current (index 0)
        player.AddMediaItem(0, placeholder);
        // Insert placeholder after current (now at index 1, so add at index 2)
        player.AddMediaItem(2, placeholder);

        // Move playback to index 1 (the real track), keeping current position
        var pos = player.CurrentPosition;
        player.SeekTo(1, pos);

        // Attach listener to intercept nav
        _audioNavListener = new AudioNavListener(this);
        player.AddListener(_audioNavListener);

        Log.Info(Tag, "Audio navigation placeholders set up");
    }

    private void OnAudioNavTransition(int newIndex)
    {
        if (_isHandlingNavTransition) return;
        _isHandlingNavTransition = true;

        if (newIndex < 1)
        {
            Log.Info(Tag, "Audio nav: previous track");
            _ = _audioPlayerService.PreviousAsync();
        }
        else if (newIndex > 1)
        {
            Log.Info(Tag, "Audio nav: next track");
            _ = _audioPlayerService.NextAsync();
        }
    }

    private sealed class AudioNavListener(BlazorPage page) : Java.Lang.Object, IPlayerListener
    {
        public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
        {
            var player = GetPlayer(page.NativeAudioPlayer);
            if (player is null) return;

            var index = player.CurrentMediaItemIndex;
            if (index != 1)
            {
                page.OnAudioNavTransition(index);
            }
        }
    }

    private static void SetImmersiveMode(Android.App.Activity activity)
    {
        var window = activity.Window;
        if (window is null) return;

#pragma warning disable CA1422, CS0618
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.SetDecorFitsSystemWindows(false);
            var controller = window.InsetsController;
            if (controller is not null)
            {
                controller.Hide(Android.Views.WindowInsets.Type.StatusBars()
                    | Android.Views.WindowInsets.Type.NavigationBars());
                controller.SystemBarsBehavior =
                    (int)Android.Views.WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }

        window.SetStatusBarColor(Android.Graphics.Color.Transparent);
        window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
        window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
        window.AddFlags(Android.Views.WindowManagerFlags.LayoutNoLimits);

        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            window.Attributes!.LayoutInDisplayCutoutMode =
                Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
        }

        window.DecorView.SystemUiFlags =
            Android.Views.SystemUiFlags.Fullscreen
            | Android.Views.SystemUiFlags.HideNavigation
            | Android.Views.SystemUiFlags.ImmersiveSticky
            | Android.Views.SystemUiFlags.LayoutFullscreen
            | Android.Views.SystemUiFlags.LayoutHideNavigation
            | Android.Views.SystemUiFlags.LayoutStable;
#pragma warning restore CA1422, CS0618
    }

    private static void SetLandscapeOrientationPlatform()
    {
        var activity = Platform.CurrentActivity;
        if (activity is not null)
        {
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;
            SetImmersiveMode(activity);
        }
    }

    private static void RestoreOrientationPlatform()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return;

        activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Unspecified;

        var window = activity.Window;
        if (window is null) return;

#pragma warning disable CA1422, CS0618
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.SetDecorFitsSystemWindows(false);
            var controller = window.InsetsController;
            controller?.Show(Android.Views.WindowInsets.Type.StatusBars()
                | Android.Views.WindowInsets.Type.NavigationBars());
        }

        window.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
        window.ClearFlags(Android.Views.WindowManagerFlags.LayoutNoLimits);

        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            window.Attributes!.LayoutInDisplayCutoutMode =
                Android.Views.LayoutInDisplayCutoutMode.Default;
        }

        window.DecorView.SystemUiFlags = Android.Views.SystemUiFlags.Visible;

        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor(Android.Graphics.Color.Transparent);
            window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
        }
#pragma warning restore CA1422, CS0618
    }

    private static IPlayer? GetPlayer(MediaElement mediaElement)
    {
        var platformView = mediaElement.Handler?.PlatformView as Android.Views.View;
        if (platformView is null)
        {
            Log.Warn(Tag, "Handler or PlatformView is null");
            return null;
        }

        var playerView = FindPlayerView(platformView);
        if (playerView is null)
        {
            Log.Warn(Tag, "PlayerView not found in view tree");
            return null;
        }

        var player = playerView.Player;
        if (player is null)
            Log.Warn(Tag, "PlayerView.Player is null");

        return player;
    }

    private static AndroidX.Media3.UI.PlayerView? FindPlayerView(Android.Views.View view)
    {
        if (view is AndroidX.Media3.UI.PlayerView pv)
            return pv;

        if (view is Android.Views.ViewGroup vg)
        {
            for (var i = 0; i < vg.ChildCount; i++)
            {
                var child = vg.GetChildAt(i);
                if (child is null) continue;
                var result = FindPlayerView(child);
                if (result is not null) return result;
            }
        }

        return null;
    }

    private void OnSwitchAudioTrack(string trackName)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var player = GetPlayer(NativePlayer);
            if (player is null) return;

            var tracks = player.CurrentTracks;
            if (tracks?.Groups is null) return;

            for (var i = 0; i < tracks.Groups.Size(); i++)
            {
                var group = (Tracks.Group)tracks.Groups.Get(i)!;
                if (group.Type != C.TrackTypeAudio)
                    continue;

                for (var j = 0; j < group.Length; j++)
                {
                    var format = group.GetTrackFormat(j);
                    if (string.Equals(format?.Label, trackName, StringComparison.OrdinalIgnoreCase)
                        || format?.Language == trackName)
                    {
                        var newParams = player.TrackSelectionParameters!
                            .BuildUpon()!
                            .SetOverrideForType(new TrackSelectionOverride(group.MediaTrackGroup, j))!
                            .Build();

                        player.TrackSelectionParameters = newParams;
                        Log.Info(Tag, $"Switched audio track to: {trackName}");
                        return;
                    }
                }
            }

            Log.Warn(Tag, $"Audio track not found: {trackName}");
        });
    }

    private void OnSwitchSubtitleTrack(string? slug)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var player = GetPlayer(NativePlayer);
            if (player is null) return;

            if (slug is null)
            {
                var disableParams = player.TrackSelectionParameters!
                    .BuildUpon()!
                    .SetTrackTypeDisabled(C.TrackTypeText, true)!
                    .Build();
                player.TrackSelectionParameters = disableParams;
                Log.Info(Tag, "Subtitles disabled");
                return;
            }

            var tracks = player.CurrentTracks;
            if (tracks?.Groups is null) return;

            // Each HLS #EXT-X-MEDIA:TYPE=SUBTITLES creates its own TrackGroup with 1 track.
            // Count text groups sequentially to find the N-th subtitle track.
            var textGroupIndex = 0;
            int? targetTextGroupOrder = null;
            if (int.TryParse(slug.AsSpan(4), out var fileStreamIndex))
            {
                // Build a mapping: the server orders subtitle tracks by index,
                // so the N-th text group corresponds to the N-th subtitle in the manifest.
                // We need to find which text group order this slug maps to.
                // The slug "sub-{N}" where N is the file stream index.
                // But we don't know the mapping from file stream index to group order here,
                // so we use the order of SubtitleTracks in the PlayerService.
                var subtitleTracks = _playerService.SubtitleTracks;
                for (var idx = 0; idx < subtitleTracks.Count; idx++)
                {
                    if (subtitleTracks[idx].Index == fileStreamIndex)
                    {
                        targetTextGroupOrder = idx;
                        break;
                    }
                }
            }

            for (var i = 0; i < tracks.Groups.Size(); i++)
            {
                var group = (Tracks.Group)tracks.Groups.Get(i)!;
                if (group.Type != C.TrackTypeText)
                    continue;

                // Try matching by Label (HLS NAME = "sub-{index}")
                for (var j = 0; j < group.Length; j++)
                {
                    var format = group.GetTrackFormat(j);
                    if (format?.Label == slug || format?.Id == slug)
                    {
                        SelectTextTrack(player, group, j, slug);
                        return;
                    }
                }

                // Match by sequential text group order
                if (targetTextGroupOrder == textGroupIndex)
                {
                    SelectTextTrack(player, group, 0, slug);
                    return;
                }

                textGroupIndex++;
            }

            Log.Warn(Tag, $"Subtitle track not found: {slug}");
        });
    }

    private static void SelectTextTrack(IPlayer player, Tracks.Group group, int trackIdx, string slug)
    {
        var newParams = player.TrackSelectionParameters!
            .BuildUpon()!
            .SetTrackTypeDisabled(C.TrackTypeText, false)!
            .SetOverrideForType(new TrackSelectionOverride(group.MediaTrackGroup, trackIdx))!
            .Build();

        player.TrackSelectionParameters = newParams;
        Log.Info(Tag, $"Switched subtitle track to: {slug} (index {trackIdx})");
    }

}
