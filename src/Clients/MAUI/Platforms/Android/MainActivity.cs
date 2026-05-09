using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace K7.Clients.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ResizeableActivity = true, LaunchMode = LaunchMode.SingleTask, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Android.Content.Intent.ActionMain], Categories = [Android.Content.Intent.CategoryLeanbackLauncher])]
public class MainActivity : MauiAppCompatActivity
{
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

        return base.DispatchKeyEvent(e);
    }

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
