using MediaClient.Shared.Domain.Interfaces;
using MediaClient.Shared.Domain.Models;
using System.Net.Http.Json;

namespace MediaClient.Shared.Services.MediaServer;

public class MediaServerService(HttpClient client) : IMediaServerService
{
    private readonly HttpClient _httpClient = client;

    public string GetBaseUrl()
        => _httpClient.BaseAddress!.OriginalString;

    public async Task<MediaDto?> GetMediaAsync(Guid id)
    {
        return await _httpClient.GetFromJsonAsync<MediaDto>($"api/medias/{id}");
    }

    public async Task<PaginatedList<MediaDto>?> GetMediasAsync(GetMediasWithPaginationQuery query)
    {
        return await _httpClient.GetFromJsonAsync<PaginatedList<MediaDto>>($"api/medias?PageNumber={query.PageNumber}&PageSize={query.PageSize}");
    }
}
