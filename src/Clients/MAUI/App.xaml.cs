using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI;

public partial class App : Application
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly BackButtonService _backButtonService;
    private readonly IK7ServerService _k7ServerService;
    private readonly ILoggerFactory _loggerFactory;

    public App(
        K7ServerManagerService k7ServerManagerService,
        IPlayerService playerService,
        IAudioPlayerService audioPlayerService,
        BackButtonService backButtonService,
        IK7ServerService k7ServerService,
        ILoggerFactory loggerFactory)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _backButtonService = backButtonService;
        _k7ServerService = k7ServerService;
        _loggerFactory = loggerFactory;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Debug.WriteLine("K7 MAUI - App.CreateWindow - start");

        K7.Clients.Shared.Services.AppReadySignal.Reset();
        var splash = new LottieSplashPage();
        var window = new Window(splash) { Title = "K7" };

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
        {
            try
            {
                Debug.WriteLine("K7 MAUI - Creating start page");
                window.Page = GetStartPage();
                Debug.WriteLine("K7 MAUI - Start page set");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"K7 MAUI - Start page creation failed: {ex}");

                // Stale/corrupt server URL after a cache reset can break BlazorPage.
                // Fall back to setup so the user can enter a fresh address.
                try
                {
                    Preferences.Remove(PreferenceKeys.K7_SERVER_URL);
                    window.Page = new SetupPage(_k7ServerManagerService, _playerService, _audioPlayerService);
                    Debug.WriteLine("K7 MAUI - Fell back to SetupPage after start failure");
                    return;
                }
                catch (Exception setupEx)
                {
                    Debug.WriteLine($"K7 MAUI - SetupPage fallback failed: {setupEx}");
                }

                window.Page = CreateStartupErrorPage(ex);
            }
        });

        Debug.WriteLine("K7 MAUI - App.CreateWindow - splash returned");
        return window;
    }

    private ContentPage GetStartPage()
    {
        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);
        Debug.WriteLine($"K7 MAUI - GetStartPage - serverUrl={(!string.IsNullOrEmpty(k7ServerUrl) ? "set" : "null")}");

        if (string.IsNullOrEmpty(k7ServerUrl))
        {
            return new SetupPage(_k7ServerManagerService, _playerService, _audioPlayerService);
        }

        try
        {
            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);
            Debug.WriteLine("K7 MAUI - GetStartPage - creating BlazorPage");
            var page = new BlazorPage(_playerService, _audioPlayerService, _backButtonService, _k7ServerService, _loggerFactory);
            Debug.WriteLine("K7 MAUI - GetStartPage - BlazorPage created");
            return page;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - BlazorPage failed, clearing server URL: {ex}");
            Preferences.Remove(PreferenceKeys.K7_SERVER_URL);
            return new SetupPage(_k7ServerManagerService, _playerService, _audioPlayerService);
        }
    }

    private static ContentPage CreateStartupErrorPage(Exception ex)
    {
        var details = $"{ex.GetType().Name}: {ex.Message}";
        if (ex.InnerException is not null)
            details += $"{Environment.NewLine}{ex.InnerException.GetType().Name}: {ex.InnerException.Message}";

        return new ContentPage
        {
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = 24,
                    Spacing = 12,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Unable to start K7",
                            FontAttributes = FontAttributes.Bold,
                            FontSize = 20,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "Please restart the app. If this keeps happening, clear app data and try again.",
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = details,
                            FontSize = 12,
                            TextColor = Colors.Gray
                        }
                    }
                }
            }
        };
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
            AppReadySignal.Reset();
#if ANDROID
            Platform.CurrentActivity?.Recreate();
#else
            Current!.Windows[0]!.Page = GetStartPage();
#endif
        });
    }
}
