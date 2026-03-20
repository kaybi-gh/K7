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
        var k7ServerUrl = BackendUrlEntry.Text?.Trim().TrimEnd('/');

        if (string.IsNullOrEmpty(k7ServerUrl) || !Uri.IsWellFormedUriString(k7ServerUrl, UriKind.Absolute))
        {
            InfoLabel.Text = "Please enter a valid address.";
            InfoLabel.IsVisible = true;
            return;
        }

        InfoLabel.Text = "Checking server...";
        InfoLabel.IsVisible = true;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await http.GetAsync($"{k7ServerUrl}/health");
            if (!response.IsSuccessStatusCode)
            {
                InfoLabel.Text = $"Server returned {(int)response.StatusCode}. Check the URL.";
                return;
            }
        }
        catch (Exception ex)
        {
            InfoLabel.Text = $"Cannot reach server: {ex.Message}";
            return;
        }

        Preferences.Set(PreferenceKeys.K7_SERVER_URL, k7ServerUrl);
        _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(k7ServerUrl) };
            var serverInfo = await http.GetStringAsync("/api/server-info");
            Preferences.Set("ServerInfo", serverInfo);
        }
        catch { }

        InfoLabel.Text = "Server saved. Restarting...";
        await Task.Delay(500);

#if ANDROID
        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#else
        Application.Current!.Quit();
#endif
    }
}
