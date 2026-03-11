using System.Net.Http.Json;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.QueryBuilders;

namespace K7.Shared.Services;

public class K7ServerService : IK7ServerService
{
    public HttpClient HttpClient { get; }
    private readonly JsonSerializerOptions _serializerOptions;

    public K7ServerService(HttpClient httpClient)
    {
        HttpClient = httpClient;

        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNameCaseInsensitive = true
        };
        _serializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public Uri? GetAbsoluteUri(string? relativePath = null)
    {
        return HttpClient.BaseAddress != null && !string.IsNullOrEmpty(relativePath)
            ? new Uri(HttpClient.BaseAddress, relativePath)
            : HttpClient.BaseAddress;
    }

    public async Task<Guid> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken = default)
    {
        var requestUri = CreateDeviceRequestUriBuilder.Route;
        var responseMessage = await HttpClient.PostAsJsonAsync(requestUri, request, _serializerOptions);
        responseMessage.EnsureSuccessStatusCode();
        var result = await responseMessage.Content.ReadFromJsonAsync<GetDeviceQuery>(_serializerOptions, cancellationToken);
        return result!.Id;
    }

    public async Task AttachCurrentUserToDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var responseMessage = await HttpClient.PostAsync($"/api/devices/{deviceId}/attach-user", null, cancellationToken);
        responseMessage.EnsureSuccessStatusCode();
    }

    public async Task<PaginatedListDto<DeviceDto>?> GetDevicesAsync(GetDevicesQuery? query = null, CancellationToken cancellationToken = default)
    {
        var requestUri = GetDevicesQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<DeviceDto>>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<List<MediaFormatDto>> GetMediaFormatsAsync(CancellationToken cancellationToken = default)
    {
        var formats = await HttpClient.GetFromJsonAsync<List<MediaFormatDto>>("api/media-formats", _serializerOptions, cancellationToken);
        return formats ?? [];
    }

    public async Task<MovieDto?> GetMovieAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<MovieDto>($"api/medias/{id}", _serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public async Task<PaginatedListDto<LiteMediaDto>?> GetLiteMediasAsync(GetMediasWithPaginationQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = GetMediasWithPaginationQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<LiteMediaDto>>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<MediaDto?> GetMediaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<MediaDto>($"api/medias/{id}", _serializerOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public async Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<PersonDto>($"api/persons/{id}", _serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<PersonDto>?> GetPersonsAsync(GetPersonsWithPaginationQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = GetPersonsWithPaginationQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<PersonDto>>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<IndexedFileStreamUri?> GetIndexedFileStreamUriAsync(GetIndexedFileStreamsUriQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = GetIndexedFileStreamsUriQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<IndexedFileStreamUri>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<StreamingSessionDto?> CreateStreamSessionAsync(CreateStreamSessionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/stream-sessions", request, _serializerOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Creating stream session failed with status {response.StatusCode}: {content}");
        }

        return await response.Content.ReadFromJsonAsync<StreamingSessionDto>(_serializerOptions, cancellationToken);
    }

    public async Task ReportPlaybackProgressAsync(Guid mediaId, Guid sessionId, double position, double duration, CancellationToken cancellationToken = default)
    {
        var payload = new { MediaId = mediaId, SessionId = sessionId, Position = position, Duration = duration };
        var response = await HttpClient.PostAsJsonAsync("api/medias/playback-progress", payload, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year = null, string? providerId = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"query={Uri.EscapeDataString(query)}" };
        
        if (year.HasValue)
        {
            queryParams.Add($"year={year.Value}");
        }

        if (!string.IsNullOrWhiteSpace(providerId))
        {
            queryParams.Add($"providerId={Uri.EscapeDataString(providerId)}");
        }

        var queryString = string.Join("&", queryParams);
        var formats = await HttpClient.GetFromJsonAsync<IEnumerable<MetadataSearchResult>>($"api/metadata/search?{queryString}", _serializerOptions, cancellationToken);
        return formats ?? [];
    }

    public async Task ReidentifyIndexedFileAsync(Guid id, ReidentifyIndexedFileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/indexed-files/{id}/reidentify", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReidentifyMediaAsync(Guid id, ReidentifyMediaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/medias/{id}/reidentify", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<LibraryDto>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = await HttpClient.GetFromJsonAsync<List<LibraryDto>>("api/libraries", _serializerOptions, cancellationToken);
        return libraries ?? [];
    }

    public async Task<Guid> CreateLibraryAsync(CreateLibraryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/libraries", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(path)
            ? "api/filesystem/directories"
            : $"api/filesystem/directories?path={Uri.EscapeDataString(path)}";
        return await HttpClient.GetFromJsonAsync<DirectoryContentDto>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<LitePlaylistDto>?> GetPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<LitePlaylistDto>>(
            $"api/playlists?pageNumber={pageNumber}&pageSize={pageSize}", _serializerOptions, cancellationToken);
    }

    public async Task<PlaylistDto?> GetPlaylistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<PlaylistDto>($"api/playlists/{id}", _serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<PlaylistItemDto>?> GetPlaylistItemsAsync(Guid playlistId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<PlaylistItemDto>>(
            $"api/playlists/{playlistId}/items?pageNumber={pageNumber}&pageSize={pageSize}", _serializerOptions, cancellationToken);
    }

    public async Task<Guid> CreatePlaylistAsync(CreatePlaylistRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/playlists", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task UpdatePlaylistAsync(Guid id, UpdatePlaylistRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/playlists/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePlaylistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/playlists/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddPlaylistItemAsync(Guid playlistId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/playlists/{playlistId}/items", new { MediaId = mediaId }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task RemovePlaylistItemAsync(Guid playlistId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/playlists/{playlistId}/items/{itemId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PaginatedListDto<LiteSmartPlaylistDto>?> GetSmartPlaylistsAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<LiteSmartPlaylistDto>>(
            $"api/smart-playlists?pageNumber={pageNumber}&pageSize={pageSize}", _serializerOptions, cancellationToken);
    }

    public async Task<SmartPlaylistDto?> GetSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<SmartPlaylistDto>($"api/smart-playlists/{id}", _serializerOptions, cancellationToken);
    }

    public async Task<Guid> CreateSmartPlaylistAsync(CreateSmartPlaylistRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/smart-playlists", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task UpdateSmartPlaylistAsync(Guid id, UpdateSmartPlaylistRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/smart-playlists/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/smart-playlists/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task EvaluateSmartPlaylistAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/smart-playlists/{id}/evaluate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
