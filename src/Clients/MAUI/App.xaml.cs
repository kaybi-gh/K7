using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
#if ANDROID
using K7.Clients.MAUI.Platforms.Android;
#endif

namespace K7.Clients.MAUI;

public partial class App : Application
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly BackButtonService _backButtonService;
    private readonly IK7ServerService _k7ServerService;

    public App(
        K7ServerManagerService k7ServerManagerService,
        IPlayerService playerService,
        IAudioPlayerService audioPlayerService,
        BackButtonService backButtonService,
        IK7ServerService k7ServerService)
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

        K7.Clients.Shared.Services.AppReadySignal.Reset();
        MauiStartupVisual.Reset();
        StartSessionRestore();
#if ANDROID
        _startPageAssigned = false;
#endif

        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(8), () =>
        {
            MauiStartupVisual.NotifyFirstFrame();
            MauiStartupVisual.NotifyStartPageSet();
        });

#if ANDROID
        var placeholder = new ContentPage { BackgroundColor = Color.FromArgb("#0d0907") };
        var window = new Window(placeholder) { Title = "K7" };
        AndroidStartupLottieOverlay.ReadyToBuildStartPage += () => AssignStartPage(window);
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(6), () => AssignStartPage(window));
        Debug.WriteLine("K7 MAUI - App.CreateWindow - placeholder returned");
        return window;
#else
        ContentPage page;
        try
        {
            page = GetStartPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - Start page creation failed: {ex}");
            page = CreateStartupErrorPage(ex);
        }

        page.Loaded += OnStartPageLoaded;
        var window = new Window(page) { Title = "K7" };
        Debug.WriteLine("K7 MAUI - App.CreateWindow - start page returned");
        return window;
#endif
    }

#if ANDROID
    private bool _startPageAssigned;

    private void AssignStartPage(Window window)
    {
        if (_startPageAssigned)
            return;

        _startPageAssigned = true;
        ContentPage page;
        try
        {
            page = GetStartPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - Start page creation failed: {ex}");
            page = CreateStartupErrorPage(ex);
        }

        page.Loaded += OnStartPageLoaded;
        window.Page = page;
        AndroidStartupLottieOverlay.ResumeTicker();
    }
#endif

    private static void OnStartPageLoaded(object? sender, EventArgs e)
    {
        if (sender is Page page)
            page.Loaded -= OnStartPageLoaded;

        MauiStartupVisual.NotifyFirstFrame();
        if (sender is not BlazorPage)
        {
#if ANDROID
            AndroidStartupLottieOverlay.Dismiss();
#endif
        }

        Application.Current?.Dispatcher.DispatchDelayed(
            TimeSpan.FromSeconds(2),
            MauiStartupVisual.NotifyStartPageSet);
    }

    private static void StartSessionRestore()
    {
        var auth = IPlatformApplication.Current?.Services.GetService<AuthenticationStateProvider>();
        auth?.GetAuthenticationStateAsync().FireAndForget();
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
            var page = new BlazorPage(_playerService, _audioPlayerService, _backButtonService, _k7ServerService);
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
            MauiStartupVisual.Reset();
#if ANDROID
            Platform.CurrentActivity?.Recreate();
#else
            Current!.Windows[0]!.Page = GetStartPage();
#endif
        });
    }
}
