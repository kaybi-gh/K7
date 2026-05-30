using System.Net.Http.Json;
using System.Net.Http.Headers;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Entities;

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

    public async Task<IReadOnlyList<PeerLibraryDto>> GetRemoteLibrariesAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/libraries";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PeerLibraryDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<PeerMediaDto>> GetRemoteMediaAsync(string baseUrl, string accessToken, Guid libraryId, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/libraries/{libraryId}/media";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PeerMediaDto>>(cancellationToken) ?? [];
    }

    private sealed record TokenResponse(string? AccessToken);
}
