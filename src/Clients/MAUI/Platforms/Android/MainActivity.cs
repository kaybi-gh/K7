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
    private long _selectDownTime;
    private bool _selectLongPressFired;
    private bool _mediaLibraryStarted;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

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

        // Media3/ExoPlayer init is heavy. Wait until the start page has painted so
        // we do not freeze the overlay Lottie.
        MauiStartupVisual.StartPageSet += OnStartPageSet;

        AndroidStartupLottieOverlay.Show(this);
    }

    private void OnStartPageSet()
    {
        MauiStartupVisual.StartPageSet -= OnStartPageSet;
        if (_mediaLibraryStarted || IsFinishing)
            return;

        _mediaLibraryStarted = true;
        Window?.DecorView.Post(() =>
        {
            StartService(new Intent(this, typeof(K7MediaLibraryService)));
        });
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
        if (e is not null && IsDpadNavigationKey(e.KeyCode))
        {
            var dpadPage = GetBlazorPage();
            if (dpadPage?.TryForwardTvVideoDpad(e) == true)
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

    private static BlazorPage? GetBlazorPage()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.Count > 0)
        {
            var window = Microsoft.Maui.Controls.Application.Current.Windows[0];
            return window.Page as BlazorPage;
        }
        return null;
    }

    protected override void OnDestroy()
    {
        MauiStartupVisual.StartPageSet -= OnStartPageSet;
        AndroidStartupLottieOverlay.Dismiss();
        base.OnDestroy();
    }
}
