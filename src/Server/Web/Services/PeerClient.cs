using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

public class PeerClient(HttpClient httpClient, ILogger<PeerClient> logger) : IPeerClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
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

    public async Task SendPeerRejectAsync(string requesterUrl, string providerUrl, CancellationToken cancellationToken = default)
    {
        var url = $"{requesterUrl.TrimEnd('/')}/api/federation/peer-reject";
        var payload = new { ProviderUrl = providerUrl };
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
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Token request to {Url} failed with {StatusCode}: {ErrorBody}", url, (int)response.StatusCode, errorBody);
            return null;
        }

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

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PeerLibraryDto>>(_jsonOptions, cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<PeerMediaDto>> GetRemoteMediaAsync(string baseUrl, string accessToken, Guid libraryId, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/libraries/{libraryId}/media";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<PeerMediaDto>>(_jsonOptions, cancellationToken) ?? [];
    }

    public async Task<StreamingSessionDto?> CreateRemoteStreamSessionAsync(string baseUrl, string accessToken, CreateFederationStreamSessionRequest request, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/stream-sessions";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Content = JsonContent.Create(request, options: _jsonOptions);

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to create remote stream session on {BaseUrl}: {StatusCode}", baseUrl, (int)response.StatusCode);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<StreamingSessionDto>(_jsonOptions, cancellationToken);
    }

    public async Task<HttpResponseMessage> ProxyStreamContentAsync(string baseUrl, string accessToken, Guid sessionId, string path, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/stream-sessions/{sessionId}/{path}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    public async Task<PeerFullMediaMetadataDto?> GetRemoteMediaMetadataAsync(string baseUrl, string accessToken, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/media/{mediaId}/metadata";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("GetRemoteMediaMetadataAsync failed with {StatusCode} for media {MediaId}", (int)response.StatusCode, mediaId);
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("GetRemoteMediaMetadataAsync received non-JSON response ({ContentType}) for media {MediaId}", contentType, mediaId);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PeerFullMediaMetadataDto>(_jsonOptions, cancellationToken);
    }

    public async Task<IndexedFileDto?> GetRemoteFileDetailsAsync(string baseUrl, string accessToken, Guid fileId, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/indexed-files/{fileId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<IndexedFileDto>(_jsonOptions, cancellationToken);
    }

    public async Task NotifyMediaAsync(string baseUrl, string accessToken, Guid libraryId, Guid mediaId, PeerMediaNotificationType type, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/media-notify";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { LibraryId = libraryId, MediaId = mediaId, Type = type });

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyRevocationAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/revoke-notify";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyProviderRevocationAsync(string requesterUrl, string providerUrl, CancellationToken cancellationToken = default)
    {
        var url = $"{requesterUrl.TrimEnd('/')}/api/federation/provider-revoke-notify";
        var payload = new { ProviderUrl = providerUrl };
        var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyShareUpdateAsync(string baseUrl, string accessToken, IReadOnlyList<Guid> sharedLibraryIds, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/share-update-notify";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { SharedLibraryIds = sharedLibraryIds });

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> PingAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default)
    {
        var url = $"{baseUrl.TrimEnd('/')}/api/federation/ping";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> IsReachableAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{baseUrl.TrimEnd('/')}/.well-known/openid-configuration";
            var response = await httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken);
}
