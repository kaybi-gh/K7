using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Util;
using AndroidX.Core.View;
using K7.Clients.MAUI.Platforms.Android.Services;

namespace K7.Clients.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ResizeableActivity = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Android.Content.Intent.ActionMain], Categories = [Android.Content.Intent.CategoryLeanbackLauncher])]
public class MainActivity : MauiAppCompatActivity
{
    private const string RemoteLogTag = "K7-Remote";
    private long _selectDownTime;
    private bool _selectLongPressFired;

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

        // Start the MediaLibraryService so audio playback and notification controls work
        // even without an Android Auto connection. Media3 auto-promotes to foreground on playback.
        var serviceIntent = new Intent(this, typeof(K7MediaLibraryService));
        StartService(serviceIntent);
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

        if (e is not null && IsSelectKeyCode(e.KeyCode))
        {
            var selectPage = GetBlazorPage();
            if (selectPage is not null)
            {
                if (TryHandleTvSelectKey(e, selectPage))
                    return true;
            }
        }

        return base.DispatchKeyEvent(e);
    }

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
                    Log.Info(RemoteLogTag, "select long key={0} held={1}", (int)e.KeyCode, heldRepeat);
                    page.NotifyTvRemoteSelect("long", (int)e.KeyCode, heldRepeat);
                    return true;
                }

                _selectDownTime = e.EventTime;
                _selectLongPressFired = false;
                Log.Info(RemoteLogTag, "select down key={0}", (int)e.KeyCode);
                page.NotifyTvRemoteSelect("down", (int)e.KeyCode, 0);
                return true;

            case KeyEventActions.Up:
                var heldMs = e.EventTime - _selectDownTime;
                var phase = _selectLongPressFired ? "long-up" : "up";
                Log.Info(RemoteLogTag, "select {0} key={1} held={2}", phase, (int)e.KeyCode, heldMs);
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
}
