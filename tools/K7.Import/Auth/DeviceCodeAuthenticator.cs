using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace K7.Import.Auth;

public sealed class DeviceCodeAuthenticator
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    private string? _accessToken;

    public DeviceCodeAuthenticator(string serverUrl)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _httpClient = new HttpClient();
    }

    public string? AccessToken => _accessToken;

    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        var deviceResponse = await RequestDeviceCodeAsync(cancellationToken);

        var verifyUrl = $"{deviceResponse.VerificationUri}?user_code={Uri.EscapeDataString(deviceResponse.UserCode)}";
        AnsiConsole.MarkupLine($"[bold yellow]Open this URL to authorize:[/] {verifyUrl}");
        AnsiConsole.WriteLine();

        _accessToken = await PollForTokenAsync(deviceResponse.DeviceCode, deviceResponse.Interval, cancellationToken);
    }

    private async Task<DeviceCodeResponse> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = "k7-cli",
            ["scope"] = "openid profile email roles api"
        });

        var response = await _httpClient.PostAsync($"{_serverUrl}/connect/device", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(cancellationToken: cancellationToken);
        return result ?? throw new InvalidOperationException("Failed to get device code response.");
    }

    private async Task<string> PollForTokenAsync(string deviceCode, int intervalSeconds, CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(intervalSeconds, 5));

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(interval, cancellationToken);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "k7-cli",
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = deviceCode
            });

            var response = await _httpClient.PostAsync($"{_serverUrl}/connect/token", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
                return tokenResponse?.AccessToken ?? throw new InvalidOperationException("Token response missing access_token.");
            }

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);

            if (error?.Error is "authorization_pending")
                continue;

            if (error?.Error is "slow_down")
            {
                interval += TimeSpan.FromSeconds(5);
                continue;
            }

            throw new InvalidOperationException($"Device code flow failed: {error?.Error} - {error?.ErrorDescription}");
        }

        throw new OperationCanceledException();
    }

    private sealed record DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; init; } = "";

        [JsonPropertyName("user_code")]
        public string UserCode { get; init; } = "";

        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; init; } = "";

        [JsonPropertyName("interval")]
        public int Interval { get; init; } = 5;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }
    }

    private sealed record ErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; init; }
    }
}
