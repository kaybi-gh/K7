using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.MAUI;

public partial class App : Application
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;

    public App(K7ServerManagerService k7ServerManagerService, IPlayerService playerService, IAudioPlayerService audioPlayerService)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
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
                    window.Page = GetNavigationPage();
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
        var navigationPage = GetNavigationPage();
        Debug.WriteLine("K7 MAUI - App.CreateWindow - page created");
        return new Window(navigationPage)
        {
            Title = "K7"
        };
#endif
    }

    private NavigationPage GetNavigationPage()
    {
        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        Debug.WriteLine($"K7 MAUI - GetNavigationPage - serverUrl={(!string.IsNullOrEmpty(k7ServerUrl) ? "set" : "null")}");

        ContentPage? page;
        if (string.IsNullOrEmpty(k7ServerUrl))
        {
            page = new SetupPage(_k7ServerManagerService, _playerService, _audioPlayerService);
        }
        else
        {
            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);
            Debug.WriteLine("K7 MAUI - GetNavigationPage - creating BlazorPage");
            page = new BlazorPage(_playerService, _audioPlayerService);
            Debug.WriteLine("K7 MAUI - GetNavigationPage - BlazorPage created");
        }

        return new NavigationPage(page);
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
            Current!.Windows[0]!.Page = GetNavigationPage();
        });
    }
}
