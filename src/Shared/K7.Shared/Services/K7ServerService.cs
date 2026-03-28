using System.Net.Http.Json;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Users;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.QueryBuilders;

namespace K7.Shared.Services;

public class K7ServerService : IK7ServerService, IMediaService, ILibraryService, IPlaylistService, IStreamingService, IDeviceApiService, IUserAdminService, IRatingService, IServerInfoService
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

    public async Task RateMediaAsync(Guid mediaId, int value, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/medias/{mediaId}/rating", new { Value = value }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MusicStatsDto?> GetMusicStatsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<MusicStatsDto>("api/music/stats", _serializerOptions, cancellationToken);
    }

    public async Task<List<MediaDto>?> GetMusicRadioAsync(string type, Guid? seedTrackId = null, Guid? seedArtistId = null, string? moodPreset = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"type={Uri.EscapeDataString(type)}" };
        if (seedTrackId.HasValue) queryParams.Add($"seedTrackId={seedTrackId.Value}");
        if (seedArtistId.HasValue) queryParams.Add($"seedArtistId={seedArtistId.Value}");
        if (moodPreset is not null) queryParams.Add($"moodPreset={Uri.EscapeDataString(moodPreset)}");
        if (limit != 50) queryParams.Add($"limit={limit}");
        var url = $"api/music/radio?{string.Join("&", queryParams)}";
        return await HttpClient.GetFromJsonAsync<List<MediaDto>>(url, _serializerOptions, cancellationToken);
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

    public async Task UpdateLibraryAsync(Guid libraryId, UpdateLibraryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/libraries/{libraryId}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteLibraryAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/libraries/{libraryId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task IndexLibraryFilesAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/libraries/{libraryId}/index-files", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(path)
            ? "api/filesystem/directories"
            : $"api/filesystem/directories?path={Uri.EscapeDataString(path)}";
        return await HttpClient.GetFromJsonAsync<DirectoryContentDto>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<List<MetadataProviderInfoDto>> GetMetadataProvidersAsync(LibraryMediaType? mediaType = null, CancellationToken cancellationToken = default)
    {
        var requestUri = mediaType.HasValue
            ? $"api/metadata-providers?mediaType={mediaType.Value}"
            : "api/metadata-providers";
        var providers = await HttpClient.GetFromJsonAsync<List<MetadataProviderInfoDto>>(requestUri, _serializerOptions, cancellationToken);
        return providers ?? [];
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

    public async Task<ServerInfoDto?> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<ServerInfoDto>("api/server-info", _serializerOptions, cancellationToken);
    }

    public async Task UpdateDefaultLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/admin/settings/default-language", new { Language = language }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AuthenticationInfoDto?> GetAuthenticationInfoAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<AuthenticationInfoDto>("api/admin/authentication-info", _serializerOptions, cancellationToken);
    }

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await HttpClient.GetFromJsonAsync<List<UserDto>>("api/users", _serializerOptions, cancellationToken);
        return users ?? [];
    }

    public async Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await HttpClient.GetFromJsonAsync<UserDto>("api/users/me", _serializerOptions, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/role", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateUserCapabilitiesAsync(Guid userId, UpdateUserCapabilitiesRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/capabilities", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/users/{userId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ToggleUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/active", new { IsActive = isActive }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateUserLibraryExclusionsAsync(Guid userId, UpdateUserLibraryExclusionsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/library-exclusions", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateUserMediaExclusionsAsync(Guid userId, UpdateUserMediaExclusionsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/media-exclusions", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ToggleMediaExclusionAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/users/me/media-exclusions/{mediaId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ToggleExclusionResponse>(_serializerOptions, cancellationToken);
        return result?.Excluded ?? false;
    }

    private sealed record ToggleExclusionResponse(bool Excluded);

    public async Task UpdateUserPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/pin", new { Pin = pin }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetUserLanguageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await HttpClient.GetFromJsonAsync<UserLanguageResponse>("api/users/me/language", _serializerOptions, cancellationToken);
            return result?.Language;
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateUserLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/language", new { Language = language }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record UserLanguageResponse(string? Language);

    public async Task<List<ContentRestrictionProfileDto>> GetContentRestrictionProfilesAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync("api/restriction-profiles", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ContentRestrictionProfileDto>>(_serializerOptions, cancellationToken) ?? [];
    }

    public async Task<Guid> CreateContentRestrictionProfileAsync(CreateContentRestrictionProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/restriction-profiles", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task UpdateContentRestrictionProfileAsync(Guid id, UpdateContentRestrictionProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/restriction-profiles/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteContentRestrictionProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/restriction-profiles/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignContentRestrictionProfileAsync(Guid userId, Guid? profileId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/users/{userId}/restriction-profile", new { ProfileId = profileId }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<RestrictedMediaPreviewDto>> PreviewRestrictedMediasAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<RestrictedMediaPreviewDto>>($"api/restriction-profiles/{profileId}/restricted-medias", _serializerOptions, cancellationToken) ?? [];
    }
}
