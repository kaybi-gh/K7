using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using K7.Clients.MAUI.Platforms.Android;
using K7.Clients.MAUI.Platforms.Android.Services;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ResizeableActivity = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Android.Content.Intent.ActionMain], Categories = [Android.Content.Intent.CategoryLeanbackLauncher])]
public class MainActivity : MauiAppCompatActivity
{
    private const int MediaLibraryIdleDelayMs = 2000;
    private static readonly TimeSpan MediaLibraryStartFailsafe = TimeSpan.FromSeconds(30);
    private long _selectDownTime;
    private bool _selectLongPressFired;
    private bool _mediaLibraryStarted;
    private CancellationTokenSource? _mediaLibraryStartCts;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        MauiStartupTrace.Mark("MainActivity.OnCreate-start");
        // Ignore restored view state. After TV/process death MAUI Blazor paints the
        // last page (often select-profile) with a dead JS circuit: remote and taps
        // do nothing until a force-stop. A clean start matches Restart().
        MauiStartupTrace.Mark("base.OnCreate-start");
        base.OnCreate(null);
        MauiStartupTrace.Mark("base.OnCreate-end");
        // Process survived (media service). Drop a window whose destroy threw so it
        // cannot keep receiving the remote under the new select-profile page.
        AndroidBlazorHost.CloseStaleWindows(this);

        // Logo overlay ASAP - before permission prompts / window chrome - so cold
        // start is not a long blank brand color.
        AndroidStartupLottieOverlay.Show(this);

        if (Window is not null)
        {
            WindowCompat.SetDecorFitsSystemWindows(Window, false);

#pragma warning disable CA1422 // Validate platform compatibility
            if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            {
                Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
                Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
            }
#pragma warning restore CA1422
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(33)
            && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions([Android.Manifest.Permission.PostNotifications], requestCode: 0);
        }

        // Media3/ExoPlayer init and the service's DI graph are heavy. Start the service only
        // after the first Blazor paint plus an idle delay: at StartPageSet it landed on the
        // main thread in the middle of first render (24 s stall and an ANR on TV).
        _mediaLibraryStartCts = new CancellationTokenSource();
        _ = StartMediaLibraryAfterFirstPaintAsync(_mediaLibraryStartCts.Token);
    }

    private async Task StartMediaLibraryAfterFirstPaintAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var failsafe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            failsafe.CancelAfter(MediaLibraryStartFailsafe);
            try
            {
                await K7.Clients.Shared.Services.FirstPaintSignal.WaitAsync(failsafe.Token);
                await Task.Delay(MediaLibraryIdleDelayMs, failsafe.Token);
            }
            catch (System.OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // No first paint within the failsafe (hung WebView): start anyway so media
                // controls and Android Auto still get the session.
                MauiStartupTrace.Mark("MediaLibrary-start-failsafe");
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            // Build the service's singleton graph on a worker thread so OnCreate (main thread)
            // only pays for the Media3 objects, which must be created on the main looper.
            await Task.Run(() => K7MediaLibraryService.WarmUpDependencies(), cancellationToken);

            if (cancellationToken.IsCancellationRequested || IsFinishing || IsDestroyed || _mediaLibraryStarted)
                return;

            _mediaLibraryStarted = true;
            MauiStartupTrace.Mark("MediaLibrary-StartService");
            StartService(new Intent(this, typeof(K7MediaLibraryService)));
        }
        catch (System.OperationCanceledException)
        {
            // Activity destroyed before the service start.
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Warn("K7-Startup", "MediaLibrary deferred start failed: " + ex);
        }
    }

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e is { Action: KeyEventActions.Down })
        {
            switch (e.KeyCode)
            {
                case Keycode.Back:
                    var page = GetBlazorPage();
                    if (page is not null)
                    {
                        page.HandleBackButton();
                        return true;
                    }
                    break;
                case Keycode.MediaPlayPause or Keycode.MediaPlay or Keycode.MediaPause:
                    var playerPage = GetBlazorPage();
                    if (playerPage is not null)
                    {
                        playerPage.HandleMediaPlayPause();
                        return true;
                    }
                    break;
                case Keycode.MediaStop:
                    var stopPage = GetBlazorPage();
                    if (stopPage is not null)
                    {
                        stopPage.HandleMediaStop();
                        return true;
                    }
                    break;
            }
        }

        // Dedicated FF/RW (not D-pad). Must intercept here: native video chrome owns input
        // and these keycodes never reach Blazor or D-pad skip handling.
        if (e is not null && VideoRemoteTransportKeys.IsAndroidSkip((int)e.KeyCode))
        {
            var skipPage = GetBlazorPage();
            if (skipPage is not null
                && skipPage.TryHandleMediaSkip(
                    VideoRemoteTransportKeys.IsAndroidSkipForward((int)e.KeyCode),
                    isKeyUp: e.Action == KeyEventActions.Up,
                    isRepeat: e.Action == KeyEventActions.Down && e.RepeatCount > 0))
            {
                return true;
            }
        }

        // While video is up, deliver DPAD via EvaluateJavascript (not WebView key routing).
        // Native pass-through can go quiet after scrub/seek even when the WebView has focus.
        // After TV wake the same thing happens on select-profile: the page is painted,
        // Android focus is on another view, and the remote does nothing.
        if (e is not null && IsDpadNavigationKey(e.KeyCode))
        {
            var dpadPage = GetBlazorPage();
            if (dpadPage?.TryForwardTvVideoDpad(e) == true)
                return true;
            if (dpadPage?.TryForwardTvShellDpad(e) == true)
                return true;
        }

        if (e is not null && IsSelectKeyCode(e.KeyCode))
        {
            var selectPage = GetBlazorPage();
            if (selectPage is not null && TryHandleTvSelectKey(e, selectPage))
                return true;
        }

        return base.DispatchKeyEvent(e);
    }

    private static bool IsDpadNavigationKey(Keycode keyCode) =>
        keyCode is Keycode.DpadLeft or Keycode.DpadRight or Keycode.DpadUp or Keycode.DpadDown
            or Keycode.SystemNavigationLeft or Keycode.SystemNavigationRight
            or Keycode.SystemNavigationUp or Keycode.SystemNavigationDown;

    private bool TryHandleTvSelectKey(KeyEvent e, BlazorPage page)
    {
        switch (e.Action)
        {
            case KeyEventActions.Down:
                if (e.RepeatCount > 0)
                {
                    if (_selectLongPressFired)
                        return true;

                    var heldRepeat = e.EventTime - _selectDownTime;
                    if (heldRepeat < 600)
                        return false;

                    _selectLongPressFired = true;
                    page.NotifyTvRemoteSelect("long", (int)e.KeyCode, heldRepeat);
                    return true;
                }

                _selectDownTime = e.EventTime;
                _selectLongPressFired = false;
                page.NotifyTvRemoteSelect("down", (int)e.KeyCode, 0);
                return true;

            case KeyEventActions.Up:
                var heldMs = e.EventTime - _selectDownTime;
                // Unpaired up (process sleep / cold start) produced heldMs~4e8 and weird clicks.
                if (_selectDownTime <= 0 || heldMs < 0 || heldMs > 60_000)
                {
                    _selectDownTime = 0;
                    _selectLongPressFired = false;
                    return true;
                }

                var phase = _selectLongPressFired ? "long-up" : "up";
                page.NotifyTvRemoteSelect(phase, (int)e.KeyCode, heldMs);
                return true;

            default:
                return false;
        }
    }

    private static bool IsSelectKeyCode(Keycode keyCode) =>
        keyCode is Keycode.DpadCenter or Keycode.Enter or Keycode.NumpadEnter or Keycode.ButtonA;

    private BlazorPage? GetBlazorPage() => AndroidBlazorHost.PageFor(this);

    protected override void OnPause()
    {
        _selectDownTime = 0;
        _selectLongPressFired = false;
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        AndroidBlazorHost.CloseStaleWindows(this);
        GetBlazorPage()?.RecoverAfterHostResume();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        // OnResume runs before the window can take focus, so RequestFocus there is a no-op
        // and the remote stays dead on the painted select-profile page.
        if (hasFocus)
            GetBlazorPage()?.RestoreInputAfterWindowFocus();
    }

    protected override void OnDestroy()
    {
        _mediaLibraryStartCts?.Cancel();
        _mediaLibraryStartCts = null;
        AndroidStartupLottieOverlay.Dismiss();
        base.OnDestroy();
    }
}
