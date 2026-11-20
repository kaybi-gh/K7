using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Shares;
using K7.Shared.Interfaces;
using K7.Shared.Json;

namespace K7.Shared.Services;

public class PublicShareService(HttpClient httpClient) : IPublicShareService
{
    /// <summary>
    /// Name of the HttpClient to register for this service. It must not carry the handlers
    /// of the signed-in client (sign-in redirect, shared profile header).
    /// </summary>
    public const string HttpClientName = "K7PublicShare";

    private readonly JsonSerializerOptions _serializerOptions = K7JsonSerializerOptions.CreateDefault();

    public async Task<OpenShareResultDto?> OpenAsync(string token, string? password, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/public/shares/open",
            new OpenShareRequest { Token = token, Password = password },
            _serializerOptions,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenShareResultDto>(_serializerOptions, cancellationToken);
    }

    public async Task<OpenShareResultDto?> GetSessionAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/public/shares/{shareId}/session", cancellationToken);
        if (IsSessionGone(response.StatusCode))
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenShareResultDto>(_serializerOptions, cancellationToken);
    }

    public async Task<MediaDto?> GetMediaAsync(Guid shareId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/public/shares/{shareId}/medias/{mediaId}", cancellationToken);
        if (IsSessionGone(response.StatusCode))
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MediaDto>(_serializerOptions, cancellationToken);
    }

    public async Task<StreamingSessionDto?> CreateStreamSessionAsync(
        Guid shareId,
        CreateShareStreamSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"api/public/shares/{shareId}/stream-sessions",
            request,
            _serializerOptions,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Creating share stream session failed with status {response.StatusCode}",
                inner: null,
                statusCode: response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<StreamingSessionDto>(_serializerOptions, cancellationToken);
    }

    public async Task ReportProgressAsync(
        Guid shareId,
        ReportShareProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        // Best effort: a lost report only delays what administrators see, or closing a
        // share set to close once played.
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"api/public/shares/{shareId}/progress",
                request,
                _serializerOptions,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
        }
    }

    public string GetDownloadUrl(Guid shareId, Guid indexedFileId) =>
        GetAbsoluteUri($"api/public/shares/{shareId}/files/{indexedFileId}/download")?.AbsoluteUri
        ?? string.Empty;

    public Uri? GetAbsoluteUri(string? relativePath) =>
        httpClient.BaseAddress is not null && !string.IsNullOrEmpty(relativePath)
            ? new Uri(httpClient.BaseAddress, relativePath)
            : null;

    private static bool IsSessionGone(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
