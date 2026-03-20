using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.MAUI;

public partial class SetupPage : ContentPage
{
    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;

    public SetupPage(K7ServerManagerService k7ServerManagerService, IPlayerService playerService, IAudioPlayerService audioPlayerService)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        InitializeComponent();
    }

    private async void OnValidateClicked(object sender, EventArgs e)
    {
        var k7ServerUrl = BackendUrlEntry.Text?.Trim();

        if (!string.IsNullOrEmpty(k7ServerUrl) && Uri.IsWellFormedUriString(k7ServerUrl, UriKind.Absolute))
        {
            Preferences.Set(PreferenceKeys.K7_SERVER_URL, k7ServerUrl);
            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);

            InfoLabel.Text = "Server saved. Restarting...";
            InfoLabel.IsVisible = true;
            await Task.Delay(500);

#if ANDROID
            Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#else
            Application.Current!.Quit();
#endif
        }
        else
        {
            InfoLabel.Text = "Please enter a valid address.";
            InfoLabel.IsVisible = true;
        }
    }
}
