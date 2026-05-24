using Foundation;
using K7.Clients.MAUI.Platforms.iOS.Services;

namespace K7.Clients.MAUI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Eagerly resolve audio services so they subscribe to IAudioPlayerService events
        var services = IPlatformApplication.Current?.Services;
        services?.GetService<NativeAudioService>();
        services?.GetService<NowPlayingService>();

        return result;
    }
}
