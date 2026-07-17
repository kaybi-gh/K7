using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;

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

    private void OnValidateClicked(object sender, EventArgs e) => OnValidateClickedAsync().FireAndForget();

    private async Task OnValidateClickedAsync()
    {
        var k7ServerUrl = NormalizeServerUrl(BackendUrlEntry.Text);

        if (k7ServerUrl is null)
        {
            InfoLabel.Text = "Please enter a valid address (for example k7.example.com).";
            InfoLabel.IsVisible = true;
            return;
        }

        InfoLabel.Text = $"Checking {k7ServerUrl}...";
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
        _k7ServerManagerService.EnsureOpenIddictRegistration(k7ServerUrl);

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(k7ServerUrl) };
            var serverInfo = await http.GetStringAsync("/api/server-info");
            Preferences.Set("ServerInfo", serverInfo);
        }
        catch { }

        ((App)Application.Current!).Restart();
    }

    /// <summary>
    /// Accepts a host (with optional port/path) or a full URL. Missing scheme defaults to https.
    /// </summary>
    internal static string? NormalizeServerUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var url = input.Trim().TrimEnd('/');

        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }
}
