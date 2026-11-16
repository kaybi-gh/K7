using System.Net;
using System.Net.Http.Json;
using K7.Shared.Dtos.Shares;

namespace K7.Shared.Services;

public partial class K7ServerService
{
    public async Task<MediaSharingAvailabilityDto> GetSharingAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<MediaSharingAvailabilityDto>(
            "api/shares/availability", _serializerOptions, cancellationToken);
        return result ?? new MediaSharingAvailabilityDto();
    }

    public async Task<List<MediaShareDto>> GetMediaSharesAsync(bool all = false, Guid? mediaId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/shares?all={(all ? "true" : "false")}";
        if (mediaId is { } id)
            url += $"&mediaId={id}";

        var result = await HttpClient.GetFromJsonAsync<List<MediaShareDto>>(url, _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<MediaShareDto?> GetMediaShareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"api/shares/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MediaShareDto>(_serializerOptions, cancellationToken);
    }

    public async Task<MediaShareLinkDto> CreateMediaShareAsync(CreateMediaShareRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/shares", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaShareLinkDto>(_serializerOptions, cancellationToken))!;
    }

    public async Task UpdateMediaShareAsync(Guid id, UpdateMediaShareRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/shares/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokeMediaShareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/shares/{id}/revoke", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MediaShareLinkDto> RegenerateMediaShareLinkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/shares/{id}/regenerate-link", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MediaShareLinkDto>(_serializerOptions, cancellationToken))!;
    }

    public async Task DeleteMediaShareAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/shares/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
