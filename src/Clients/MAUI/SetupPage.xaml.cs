using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Interfaces;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.MAUI;

public partial class SetupPage : ContentPage
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IMsalClientService _msalClientService;
    private readonly IPlayerService _playerService;

    public SetupPage(K7ServerManagerService k7ServerManagerService, IMsalClientService msalClientService, IPlayerService playerService)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _msalClientService = msalClientService;
        _playerService = playerService;
        InitializeComponent();
    }

    private async void OnValidateClicked(object sender, EventArgs e)
    {
        var k7ServerUrl = BackendUrlEntry.Text?.Trim();

        if (!string.IsNullOrEmpty(k7ServerUrl) && Uri.IsWellFormedUriString(k7ServerUrl, UriKind.Absolute))
        {
            Preferences.Set(PreferenceKeys.K7_SERVER_URL, k7ServerUrl);
            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);
            _msalClientService.Initialize(k7ServerUrl);
            await Navigation.PushAsync(new BlazorPage(_playerService));
        }
        else
        {
            InfoLabel.Text = "Please enter a valid address.";
            InfoLabel.IsVisible = true;
        }
    }
}
