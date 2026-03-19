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
            page = new SetupPage(_k7ServerManagerService, _playerService, _audioPlayerService);
        }
        else
        {
            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);
            page = new BlazorPage(_playerService, _audioPlayerService);
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
