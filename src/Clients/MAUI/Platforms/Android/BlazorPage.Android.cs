using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using CommunityToolkit.Maui.Views;
using K7.Clients.MAUI.Diagnostics;
using Log = Android.Util.Log;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private const string Tag = "K7-Player";

    partial void InitializePlayerPlatform()
    {
        _playerService.SwitchAudioTrackRequested += OnSwitchAudioTrack;
        _playerService.SwitchSubtitleTrackRequested += OnSwitchSubtitleTrack;
    }

    /// <summary>
    /// Enriches MediaFailed with ExoPlayer error code and HTTP response details when available.
    /// ERROR_CODE_IO_UNSPECIFIED (2000) alone is not enough - ResponseCode distinguishes 401 vs 404.
    /// DataSpecUri identifies which playlist/segment URL failed when the exception carries it.
    /// </summary>
    private string FormatAndroidPlayerErrorDetail()
    {
        try
        {
            var player = GetPlayer(NativePlayer);
            if (player?.PlayerError is not { } error)
                return string.Empty;

            var codeName = PlaybackException.GetErrorCodeName(error.ErrorCode) ?? "(unknown)";
            var detail =
                " PlayerErrorCode="
                + error.ErrorCode
                + " PlayerErrorCodeName="
                + codeName
                + " PlayerErrorMessage="
                + (error.Message ?? "(null)");

            // Parser failures (e.g. Top bit not zero) wrap IllegalStateException without DataSpec.
            // MediaItem URI is the playlist; still useful when segment DataSpec is absent.
            if (TryGetMediaItemUri(player.CurrentMediaItem, out var mediaItemUri)
                && !string.IsNullOrEmpty(mediaItemUri))
            {
                detail += " MediaItemUri=" + NativePlayerDiagnostics.RedactUrl(mediaItemUri);
            }

            Java.Lang.Throwable? cause = error.Cause;
            var depth = 0;
            var sawDataSpecUri = false;
            while (cause is not null && depth < 8)
            {
                detail +=
                    " Cause["
                    + depth
                    + "]="
                    + cause.GetType().Name
                    + ": "
                    + (cause.Message ?? "(null)");

                // Xamarin bindings flatten Java nested types (HttpDataSource$InvalidResponseCodeException).
                if (cause is HttpDataSourceInvalidResponseCodeException invalidResponse)
                {
                    detail += " ResponseCode=" + invalidResponse.ResponseCode;
                    var dataSpecUri = invalidResponse.DataSpec?.Uri?.ToString();
                    if (!string.IsNullOrEmpty(dataSpecUri))
                    {
                        detail += " DataSpecUri=" + NativePlayerDiagnostics.RedactUrl(dataSpecUri);
                        sawDataSpecUri = true;
                    }
                }
                else if (cause is HttpDataSourceHttpDataSourceException httpEx)
                {
                    var dataSpecUri = httpEx.DataSpec?.Uri?.ToString();
                    if (!string.IsNullOrEmpty(dataSpecUri))
                    {
                        detail += " DataSpecUri=" + NativePlayerDiagnostics.RedactUrl(dataSpecUri);
                        sawDataSpecUri = true;
                    }
                }
                else if (!sawDataSpecUri
                    && TryGetDataSpecUriFromCause(cause, out var reflectedUri)
                    && !string.IsNullOrEmpty(reflectedUri))
                {
                    detail += " DataSpecUri=" + NativePlayerDiagnostics.RedactUrl(reflectedUri);
                    sawDataSpecUri = true;
                }

                cause = cause.Cause;
                depth++;
            }

            if (!sawDataSpecUri)
                detail += " DataSpecUri=(none-parser-or-non-http)";

            return detail;
        }
        catch (Exception ex)
        {
            return " PlayerErrorDetailFailed=" + ex.Message;
        }
    }

    /// <summary>
    /// Parser/load failures after HTTP 200 often wrap IllegalStateException without a typed
    /// HttpDataSourceException. Reflect DataSpec when the binding exposes it on any cause.
    /// </summary>
    private static bool TryGetDataSpecUriFromCause(Java.Lang.Throwable cause, out string? uri)
    {
        uri = null;
        try
        {
            var dataSpecProp = cause.GetType().GetProperty("DataSpec");
            if (dataSpecProp?.GetValue(cause) is DataSpec dataSpec)
            {
                uri = dataSpec.Uri?.ToString();
                return !string.IsNullOrEmpty(uri);
            }
        }
        catch
        {
            // Best-effort diagnostics only.
        }

        return false;
    }

    /// <summary>
    /// Xamarin Media3 bindings expose LocalConfiguration as a nested type name that shadows the
    /// instance property, so read the playlist URI via reflection.
    /// </summary>
    private static bool TryGetMediaItemUri(MediaItem? mediaItem, out string? uri)
    {
        uri = null;
        if (mediaItem is null)
            return false;

        try
        {
            var localConfigProp = typeof(MediaItem).GetProperty("LocalConfiguration");
            var localConfig = localConfigProp?.GetValue(mediaItem);
            if (localConfig is null)
                return false;

            var uriProp = localConfig.GetType().GetProperty("Uri");
            uri = uriProp?.GetValue(localConfig)?.ToString();
            return !string.IsNullOrEmpty(uri);
        }
        catch
        {
            return false;
        }
    }

    private void EnsureAndroidWebViewTransparent()
    {
        // MAUI property mapper can re-apply an opaque BackgroundColor after the handler connects.
        // Re-assert platform transparency whenever the native player becomes visible.
        if (blazorWebView.Handler?.PlatformView is not global::Android.Webkit.WebView platformView)
        {
            NativePlayerDiagnostics.Warn(
                _nativePlayerLogger,
                "EnsureAndroidWebViewTransparent: PlatformView not ready (Handler="
                + (blazorWebView.Handler is null ? "null" : "set")
                + ")");
            return;
        }

        platformView.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
        platformView.SetBackgroundResource(0);
        platformView.Background = null;

        if (platformView.Parent is Android.Views.View parentView)
        {
            parentView.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
            parentView.SetBackgroundResource(0);
        }

        NativePlayerDiagnostics.Info(
            _nativePlayerLogger,
            "EnsureAndroidWebViewTransparent applied (WebView + parent Background=Transparent)");
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
