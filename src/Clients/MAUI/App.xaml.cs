using System.Diagnostics;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Interfaces;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.MAUI;

public partial class App : Application
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IMsalClientService _msalClientService;
    private readonly IPlayerService _playerService;

    // TODO - Use IMsalClientService in K7ServerManagerService?

    public App(K7ServerManagerService k7ServerManagerService, IMsalClientService msalClientService, IPlayerService playerService)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _msalClientService = msalClientService;
        _playerService = playerService;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var navigationPage = GetNavigationPage();
        return new Window(navigationPage)
        {
            Title = "K7"
        };
    }

    private NavigationPage GetNavigationPage()
    {
        var k7ServerUrl = Preferences.Get(PreferenceKeys.K7_SERVER_URL, null);

        ContentPage? page;
        if (string.IsNullOrEmpty(k7ServerUrl))
        {
            page = new SetupPage(_k7ServerManagerService, _msalClientService, _playerService);
        }
        else
        {
            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);
            _msalClientService.Initialize(k7ServerUrl);
            page = new BlazorPage(_playerService);
        }

        return new NavigationPage(page);
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        Debug.WriteLine($"K7 MAUI - App.xaml.cs - OnAppLinkRequestReceived - Uri: {uri}");
        if (uri.Scheme == "k7")
        {
            //_ = _authenticationService.LoginCallbackAsync(uri);
        }

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
