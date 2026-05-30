using System.Net.Http.Json;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Web.Services;

public class PeerClient(HttpClient httpClient) : IPeerClient
{
    public async Task SendPeerRequestAsync(string remoteUrl, string localServerName, string localServerUrl, string token, CancellationToken cancellationToken = default)
    {
        var url = $"{remoteUrl.TrimEnd('/')}/api/federation/peer-request";
        var payload = new { RequesterUrl = localServerUrl, RequesterName = localServerName, Token = token };
        var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendPeerConfirmAsync(string remoteUrl, string token, string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        var url = $"{remoteUrl.TrimEnd('/')}/api/federation/peer-confirm";
        var payload = new { Token = token, ClientId = clientId, ClientSecret = clientSecret };
        var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetAccessTokenAsync(string baseUrl, string clientId, string clientSecret, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/connect/token";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "peer"
        });

        var response = await httpClient.PostAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        return result?.AccessToken;
    }

    private sealed record TokenResponse(string? AccessToken);
}
