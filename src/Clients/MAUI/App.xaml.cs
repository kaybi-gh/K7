using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI;

public partial class App : Application
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly BackButtonService _backButtonService;
    private readonly IK7ServerService _k7ServerService;

    public App(K7ServerManagerService k7ServerManagerService, IPlayerService playerService, IAudioPlayerService audioPlayerService, BackButtonService backButtonService, IK7ServerService k7ServerService)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _backButtonService = backButtonService;
        _k7ServerService = k7ServerService;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Debug.WriteLine("K7 MAUI - App.CreateWindow - start");

#if ANDROID
        // Android debug: FastDev assembly loading blocks the main thread 15+ seconds.
        // Return a lightweight page immediately so Android can draw the first frame
        // and reset the ANR watchdog, then swap in the heavy BlazorPage later.
        var splash = new ContentPage
        {
            BackgroundColor = Colors.Black,
            Content = new ActivityIndicator { IsRunning = true, Color = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        var window = new Window(splash) { Title = "K7" };

        _ = Task.Run(async () =>
        {
            // Wait for the Activity to fully render the splash frame
            await Task.Delay(1500);
            Debug.WriteLine("K7 MAUI - Posting real page creation to main thread");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    Debug.WriteLine("K7 MAUI - Creating real page now");
                    window.Page = GetStartPage();
                    Debug.WriteLine("K7 MAUI - Real page set");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"K7 MAUI - Real page creation failed: {ex}");
                }
            });
        });

        Debug.WriteLine("K7 MAUI - App.CreateWindow - splash returned");
        return window;
#else
        var page = GetStartPage();
        Debug.WriteLine("K7 MAUI - App.CreateWindow - page created");
        return new Window(page)
        {
            Title = "K7"
        };
#endif
    }

    private ContentPage GetStartPage()
    {
        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        Debug.WriteLine($"K7 MAUI - GetStartPage - serverUrl={(!string.IsNullOrEmpty(k7ServerUrl) ? "set" : "null")}");

        if (string.IsNullOrEmpty(k7ServerUrl))
        {
            return new SetupPage(_k7ServerManagerService, _playerService, _audioPlayerService);
        }

        _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);
        Debug.WriteLine("K7 MAUI - GetStartPage - creating BlazorPage");
        var page = new BlazorPage(_playerService, _audioPlayerService, _backButtonService, _k7ServerService);
        Debug.WriteLine("K7 MAUI - GetStartPage - BlazorPage created");
        return page;
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        Debug.WriteLine($"K7 MAUI - App.xaml.cs - OnAppLinkRequestReceived - Uri: {uri}");
        base.OnAppLinkRequestReceived(uri);
    }

    public void Restart()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Current!.Windows[0]!.Page = GetStartPage();
        });
    }
}
