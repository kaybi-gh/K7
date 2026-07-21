using System.Text.Json;
using K7.Clients.MAUI.Constants;
using K7.Clients.MAUI.Services;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;

namespace K7.Clients.MAUI;

public partial class SetupPage : ContentPage
{
    private static readonly JsonSerializerOptions ServerInfoJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly K7ServerManagerService _k7ServerManagerService;
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private bool _busy;

    public SetupPage(K7ServerManagerService k7ServerManagerService, IPlayerService playerService, IAudioPlayerService audioPlayerService)
    {
        _k7ServerManagerService = k7ServerManagerService;
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            try { BackendUrlEntry.Focus(); }
            catch { /* ignore */ }
        };
    }

    private void OnValidateClicked(object sender, EventArgs e) => OnValidateClickedAsync().FireAndForget();

    private async Task OnValidateClickedAsync()
    {
        if (_busy)
            return;

        var k7ServerUrl = NormalizeServerUrl(BackendUrlEntry.Text);
        if (k7ServerUrl is null)
        {
            SetStatus("Please enter a valid address (for example k7.example.com or http://192.168.1.10:5000).", isError: true);
            return;
        }

        _busy = true;
        ConnectButton.IsEnabled = false;
        BackendUrlEntry.IsEnabled = false;

        try
        {
            SetStatus($"Checking {k7ServerUrl}...");

            using var cts = new CancellationTokenSource(MauiTimeouts.ServerReachability);
            var probe = await ProbeK7ServerAsync(k7ServerUrl, cts.Token);

            // HTTPS with a self-signed / LAN cert often fails; retry plain HTTP once.
            if (!probe.Success
                && k7ServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var httpUrl = "http://" + k7ServerUrl["https://".Length..];
                SetStatus($"HTTPS failed ({probe.Error}). Trying {httpUrl}...");
                using var httpCts = new CancellationTokenSource(MauiTimeouts.ServerReachability);
                probe = await ProbeK7ServerAsync(httpUrl, httpCts.Token);
                if (probe.Success)
                    k7ServerUrl = httpUrl;
            }

            if (!probe.Success)
            {
                SetStatus(
                    $"Cannot reach a K7 server at this address.{Environment.NewLine}{probe.Error}",
                    isError: true);
                return;
            }

            SetStatus("Server OK. Saving and starting...");

            Preferences.Set(PreferenceKeys.K7_SERVER_URL, k7ServerUrl);
            if (!string.IsNullOrEmpty(probe.ServerInfoJson))
                Preferences.Set("ServerInfo", probe.ServerInfoJson);

            _k7ServerManagerService.UpdateBaseAddress(k7ServerUrl);

            try
            {
                _k7ServerManagerService.EnsureOpenIddictRegistration(k7ServerUrl);
            }
            catch (Exception ex)
            {
                SetStatus(
                    $"Server is reachable, but OpenIddict setup failed:{Environment.NewLine}{ex.GetBaseException().Message}",
                    isError: true);
                return;
            }

            await Task.Yield();
            ((App)Application.Current!).Restart();
        }
        catch (OperationCanceledException)
        {
            SetStatus(
                $"Timed out after {(int)MauiTimeouts.ServerReachability.TotalSeconds}s. Check the URL and that the server is reachable from this machine.",
                isError: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Setup failed: {ex.GetBaseException().Message}", isError: true);
        }
        finally
        {
            _busy = false;
            ConnectButton.IsEnabled = true;
            BackendUrlEntry.IsEnabled = true;
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        InfoLabel.Text = message;
        InfoLabel.TextColor = isError ? Colors.IndianRed : Colors.Gray;
        InfoLabel.IsVisible = true;
    }

    private static async Task<ProbeResult> ProbeK7ServerAsync(string baseUrl, CancellationToken cancellationToken)
    {
        using var handler = CreateSetupHandler();
        using var http = new HttpClient(handler)
        {
            Timeout = MauiTimeouts.ServerReachability,
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        try
        {
            // Prefer server-info: confirms it is actually K7, not just any /health endpoint.
            using var serverInfoResponse = await http.GetAsync("api/server-info", cancellationToken);
            if (serverInfoResponse.IsSuccessStatusCode)
            {
                var json = await serverInfoResponse.Content.ReadAsStringAsync(cancellationToken);
                if (!LooksLikeK7ServerInfo(json))
                {
                    return ProbeResult.Fail(
                        "The server responded, but /api/server-info does not look like a K7 server.");
                }

                return ProbeResult.Ok(json);
            }

            // Fallback: /health proves reachability when server-info is temporarily unavailable.
            using var healthResponse = await http.GetAsync("health", cancellationToken);
            if (healthResponse.IsSuccessStatusCode)
            {
                return ProbeResult.Ok(serverInfoJson: null);
            }

            return ProbeResult.Fail(
                $"HTTP {(int)serverInfoResponse.StatusCode} from /api/server-info"
                + $" and HTTP {(int)healthResponse.StatusCode} from /health.");
        }
        catch (OperationCanceledException)
        {
            return ProbeResult.Fail(
                $"Timed out after {(int)MauiTimeouts.ServerReachability.TotalSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            return ProbeResult.Fail(ex.GetBaseException().Message);
        }
        catch (Exception ex)
        {
            return ProbeResult.Fail(ex.GetBaseException().Message);
        }
    }

    private static SocketsHttpHandler CreateSetupHandler() =>
        new()
        {
            // Fail fast on unreachable hosts instead of hanging on connect.
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

    private static bool LooksLikeK7ServerInfo(string json)
    {
        try
        {
            var info = JsonSerializer.Deserialize<ServerInfoDto>(json, ServerInfoJsonOptions);
            // Any successful deserialize of our DTO shape is enough; GuestEnabled is always present.
            return info is not null;
        }
        catch
        {
            return false;
        }
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

    private sealed record ProbeResult(bool Success, string? Error, string? ServerInfoJson)
    {
        public static ProbeResult Ok(string? serverInfoJson) => new(true, null, serverInfoJson);
        public static ProbeResult Fail(string error) => new(false, error, null);
    }
}
