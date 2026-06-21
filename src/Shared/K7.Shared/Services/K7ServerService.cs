using System.Net.Http.Json;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Users;
using K7.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Search;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.QueryBuilders;
using K7.Shared.Dtos.Home;
using K7.Shared.Enums;
using K7.Shared.Extensions;

namespace K7.Shared.Services;

public class K7ServerService : IK7ServerService, IMediaService, ILibraryService, IPlaylistService, ICollectionService, ISearchService, IStreamingService, IDeviceApiService, IUserAdminService, IRatingService, IServerInfoService, IBackgroundTaskService, IDiagnosticsService, IUserPreferencesService, IServerPreferencesService, IDownloadService, INotificationAdminService, IFederationService
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

    public Uri? GetAbsoluteUri(string? relativePath)
    {
        return HttpClient.BaseAddress is not null && !string.IsNullOrEmpty(relativePath)
            ? new Uri(HttpClient.BaseAddress, relativePath)
            : null;
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

    public async Task DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/devices/{deviceId}", cancellationToken);
        response.EnsureSuccessStatusCode();
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

    public async Task<PaginatedListDto<LiteMediaDto>?> QueryMediasAsync(QueryMediasRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(new HttpMethod("QUERY"), "api/medias")
        {
            Content = JsonContent.Create(request, options: _serializerOptions)
        };

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PaginatedListDto<LiteMediaDto>>(_serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<MediaGenreDto>?> GetMediaGenresAsync(GetMediaGenresQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = GetMediaGenresQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<MediaGenreDto>>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<MediaBrowseFacetsDto?> GetMediaBrowseFacetsAsync(GetMediaBrowseFacetsQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = GetMediaBrowseFacetsQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<MediaBrowseFacetsDto>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<string>?> GetMediaBrowseFilterSuggestionsAsync(
        GetMediaBrowseFilterSuggestionsQuery query,
        CancellationToken cancellationToken = default)
    {
        var requestUri = GetMediaBrowseFilterSuggestionsQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<IReadOnlyList<string>>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<HomeFeedItemDto>?> GetHomeFeedAsync(GetHomeFeedQuery query, CancellationToken cancellationToken = default)
    {
        var requestUri = GetHomeFeedQueryUriBuilder.Build(query);
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<HomeFeedItemDto>>(requestUri, _serializerOptions, cancellationToken);
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

    public async Task<StreamingSessionDto?> CreateRemoteStreamSessionAsync(CreateRemoteStreamSessionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/remote-stream-sessions", request, _serializerOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Creating remote stream session failed with status {response.StatusCode}: {content}");
        }

        return await response.Content.ReadFromJsonAsync<StreamingSessionDto>(_serializerOptions, cancellationToken);
    }

    public async Task ReportPlaybackProgressAsync(Guid mediaId, Guid sessionId, Guid referenceId, double position, double duration, int state, Guid? deviceId = null, CancellationToken cancellationToken = default)
    {
        var payload = new { MediaId = mediaId, SessionId = sessionId, ReferenceId = referenceId, Position = position, Duration = duration, State = state, DeviceId = deviceId };
        var response = await HttpClient.PostAsJsonAsync("api/medias/playback-progress", payload, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GenerateEphemeralTokenAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/stream-sessions/{streamSessionId}/ephemeral-token", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EphemeralTokenResponse>(_serializerOptions, cancellationToken);
        return result?.Token;
    }

    public async Task RevokeEphemeralTokenAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/stream-sessions/{streamSessionId}/ephemeral-token", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RateMediaAsync(Guid mediaId, int value, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/medias/{mediaId}/rating", new { Value = value }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SetMediaWatchStateResultDto?> SetMediaWatchStateAsync(Guid mediaId, bool watched, WatchStateScope scope = WatchStateScope.Item, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync(
            $"api/medias/{mediaId}/watch-state",
            new { Watched = watched, Scope = scope },
            _serializerOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SetMediaWatchStateResultDto>(_serializerOptions, cancellationToken);
    }

    public async Task<WatchStatsDto?> GetWatchStatsAsync(string? mediaType = null, string period = "month", DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"period={Uri.EscapeDataString(period)}" };
        if (mediaType is not null) queryParams.Add($"mediaType={Uri.EscapeDataString(mediaType)}");
        if (from is not null) queryParams.Add($"from={from.Value:O}");
        if (to is not null) queryParams.Add($"to={to.Value:O}");
        var url = $"api/stats?{string.Join("&", queryParams)}";
        return await HttpClient.GetFromJsonAsync<WatchStatsDto>(url, _serializerOptions, cancellationToken);
    }

    public async Task<PlaybackHistoryPageDto?> GetPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (mediaType is not null) queryParams.Add($"mediaType={Uri.EscapeDataString(mediaType)}");
        var url = $"api/stats/history?{string.Join("&", queryParams)}";
        return await HttpClient.GetFromJsonAsync<PlaybackHistoryPageDto>(url, _serializerOptions, cancellationToken);
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

    public async Task<IEnumerable<MetadataSearchResult>> SearchMetadataAsync(string query, int? year = null, string? providerId = null, K7.Server.Domain.Enums.MediaType? mediaType = null, CancellationToken cancellationToken = default)
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

        if (mediaType.HasValue)
        {
            queryParams.Add($"mediaType={(int)mediaType.Value}");
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

    public async Task RefreshMediaMetadataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/medias/{id}/refresh-metadata", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMediaMetadataAsync(Guid id, UpdateMediaMetadataRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/medias/{id}/metadata", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UploadMediaPictureAsync(Guid mediaId, Stream stream, string fileName, MetadataPictureType pictureType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "file", fileName);
        var response = await HttpClient.PostAsync($"api/medias/{mediaId}/pictures?pictureType={pictureType}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task DeleteMediaPictureAsync(Guid mediaId, Guid pictureId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/medias/{mediaId}/pictures/{pictureId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ProviderImageDto>> GetMediaProviderImagesAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"api/medias/{mediaId}/provider-images", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProviderImageDto>>(_serializerOptions, cancellationToken) ?? [];
    }

    public async Task<Guid> ImportMediaPictureFromUrlAsync(Guid mediaId, ImportMediaPictureFromUrlRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/medias/{mediaId}/pictures/import", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task RefreshPersonMetadataAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/persons/{id}/refresh-metadata", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePersonMetadataAsync(Guid id, UpdatePersonMetadataRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/persons/{id}/metadata", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UploadPersonPictureAsync(Guid personId, Stream stream, string fileName, MetadataPictureType pictureType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "file", fileName);
        var response = await HttpClient.PostAsync($"api/persons/{personId}/pictures?pictureType={pictureType}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task DeletePersonPictureAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/persons/{personId}/pictures", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> ImportPersonPictureFromUrlAsync(Guid personId, ImportMediaPictureFromUrlRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/persons/{personId}/pictures/import", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderImageDto>> GetPersonProviderImagesAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"api/persons/{personId}/provider-images", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<ProviderImageDto>>(_serializerOptions, cancellationToken) ?? [];
    }

    public async Task<LiteSerieEpisodeDto?> GetNextEpisodeAsync(Guid serieId, Guid currentEpisodeId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"api/medias/{serieId}/next-episode?currentEpisodeId={currentEpisodeId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LiteSerieEpisodeDto>(_serializerOptions, cancellationToken);
    }

    public async Task<List<LibraryDto>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = await HttpClient.GetFromJsonAsync<List<LibraryDto>>("api/libraries", _serializerOptions, cancellationToken);
        return libraries ?? [];
    }

    public async Task<List<LibraryGroupDto>> GetLibraryGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await HttpClient.GetFromJsonAsync<List<LibraryGroupDto>>("api/library-groups", _serializerOptions, cancellationToken);
        return groups ?? [];
    }

    public async Task<List<LibraryStatisticsDto>> GetLibraryStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<LibraryStatisticsDto>>("api/libraries/statistics", _serializerOptions, cancellationToken) ?? [];
    }

    public async Task<Guid> CreateLibraryAsync(CreateLibraryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/libraries", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task IndexLibraryFilesAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/libraries/{libraryId}/index-files", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateLibraryAsync(Guid id, UpdateLibraryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/libraries/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateLibraryGroupAsync(Guid id, UpdateLibraryGroupRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/library-groups/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteLibraryGroupAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/library-groups/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UploadLibraryGroupCoverAsync(Guid libraryGroupId, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "file", fileName);
        var response = await HttpClient.PostAsync($"api/library-groups/{libraryGroupId}/cover", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task<Guid> SetLibraryGroupCoverFromPictureAsync(Guid libraryGroupId, Guid sourcePictureId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/library-groups/{libraryGroupId}/cover?sourcePictureId={sourcePictureId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task<List<LibraryPictureDto>> GetLibraryPicturesAsync(Guid libraryId, CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<LibraryPictureDto>>($"api/libraries/{libraryId}/pictures", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task DeleteLibraryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/libraries/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<MetadataProviderInfoDto>> GetMetadataProvidersAsync(LibraryMediaType? mediaType = null, CancellationToken cancellationToken = default)
    {
        var uri = mediaType.HasValue
            ? $"api/metadata-providers?mediaType={mediaType.Value}"
            : "api/metadata-providers";
        var providers = await HttpClient.GetFromJsonAsync<List<MetadataProviderInfoDto>>(uri, _serializerOptions, cancellationToken);
        return providers ?? [];
    }

    public async Task<DirectoryContentDto?> GetDirectoriesAsync(string? path = null, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(path)
            ? "api/filesystem/directories"
            : $"api/filesystem/directories?path={Uri.EscapeDataString(path)}";
        return await HttpClient.GetFromJsonAsync<DirectoryContentDto>(requestUri, _serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<LitePlaylistDto>?> GetPlaylistsAsync(int pageNumber = 1, int pageSize = 20, MediaType? mediaType = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/playlists?pageNumber={pageNumber}&pageSize={pageSize}";
        if (mediaType.HasValue)
            url += $"&mediaType={(int)mediaType.Value}";
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<LitePlaylistDto>>(url, _serializerOptions, cancellationToken);
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

    public async Task<PaginatedListDto<LiteCollectionDto>?> GetCollectionsAsync(int pageNumber = 1, int pageSize = 20, MediaType? mediaType = null, bool? isPublic = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/collections?pageNumber={pageNumber}&pageSize={pageSize}";
        if (mediaType.HasValue)
            url += $"&mediaType={(int)mediaType.Value}";
        if (isPublic.HasValue)
            url += $"&isPublic={isPublic.Value}";
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<LiteCollectionDto>>(url, _serializerOptions, cancellationToken);
    }

    public async Task<CollectionDto?> GetCollectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<CollectionDto>($"api/collections/{id}", _serializerOptions, cancellationToken);
    }

    public async Task<PaginatedListDto<CollectionItemDto>?> GetCollectionItemsAsync(Guid collectionId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<CollectionItemDto>>(
            $"api/collections/{collectionId}/items?pageNumber={pageNumber}&pageSize={pageSize}", _serializerOptions, cancellationToken);
    }

    public async Task<Guid> CreateCollectionAsync(CreateCollectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/collections", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task UpdateCollectionAsync(Guid id, UpdateCollectionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/collections/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCollectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/collections/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> AddCollectionItemAsync(Guid collectionId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/collections/{collectionId}/items", new { MediaId = mediaId }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task RemoveCollectionItemAsync(Guid collectionId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/collections/{collectionId}/items/{itemId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<GlobalSearchResultDto?> GlobalSearchAsync(string q, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<GlobalSearchResultDto>($"api/search?q={Uri.EscapeDataString(q)}&pageSize={pageSize}", _serializerOptions, cancellationToken);
    }

    public async Task<AboutInfoDto?> GetAboutInfoAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<AboutInfoDto>("api/about", _serializerOptions, cancellationToken);
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

    public async Task UpdateDefaultThemeAsync(string theme, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/admin/settings/default-theme", new { Theme = theme }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ActiveStreamDto>?> GetActiveStreamsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<ActiveStreamDto>>("api/admin/streams", _serializerOptions, cancellationToken);
    }

    public async Task<ServerMetricsHistoryDto?> GetServerMetricsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<ServerMetricsHistoryDto>("api/admin/metrics", _serializerOptions, cancellationToken);
    }

    public async Task<PlaybackHistoryPageDto?> GetAdminPlaybackHistoryAsync(int page = 1, int pageSize = 25, string? mediaType = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (mediaType is not null) queryParams.Add($"mediaType={Uri.EscapeDataString(mediaType)}");
        if (userId.HasValue) queryParams.Add($"userId={userId.Value}");
        var url = $"api/admin/stats/history?{string.Join("&", queryParams)}";
        return await HttpClient.GetFromJsonAsync<PlaybackHistoryPageDto>(url, _serializerOptions, cancellationToken);
    }

    public async Task<WatchStatsDto?> GetAdminWatchStatsAsync(string? mediaType = null, string period = "month", Guid? userId = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"period={Uri.EscapeDataString(period)}" };
        if (mediaType is not null) queryParams.Add($"mediaType={Uri.EscapeDataString(mediaType)}");
        if (userId.HasValue) queryParams.Add($"userId={userId.Value}");
        if (from.HasValue) queryParams.Add($"from={from.Value:O}");
        if (to.HasValue) queryParams.Add($"to={to.Value:O}");
        var url = $"api/admin/stats?{string.Join("&", queryParams)}";
        return await HttpClient.GetFromJsonAsync<WatchStatsDto>(url, _serializerOptions, cancellationToken);
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

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/users", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>(_serializerOptions, cancellationToken))!;
    }

    public async Task MergeUsersAsync(Guid sourceUserId, Guid targetUserId, MergeStrategy? strategy = null, CancellationToken cancellationToken = default)
    {
        var body = strategy is not null ? new MergeUsersRequest { Strategy = strategy } : null;
        var response = await HttpClient.PostAsJsonAsync($"api/users/{sourceUserId}/merge-into/{targetUserId}", body, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetUserPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/users/{userId}/reset-password", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/profile", request, _serializerOptions, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    public async Task UploadAvatarAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetImageContentType(fileName));
        content.Add(streamContent, "file", fileName);
        var response = await HttpClient.PostAsync("api/users/me/avatar", content, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    private static string GetImageContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }

    public async Task RemoveAvatarAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync("api/users/me/avatar", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/password", request, _serializerOptions, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    public async Task SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/users/me/password", request, _serializerOptions, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    public async Task RemovePasswordAsync(RemovePasswordRequest request, CancellationToken cancellationToken = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Delete, "api/users/me/password")
        {
            Content = JsonContent.Create(request, options: _serializerOptions)
        };
        var response = await HttpClient.SendAsync(msg, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    public async Task UpdateEmailAsync(UpdateEmailRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/email", request, _serializerOptions, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    public async Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Delete, "api/users/me")
        {
            Content = JsonContent.Create(request, options: _serializerOptions)
        };
        var response = await HttpClient.SendAsync(msg, cancellationToken);
        await response.EnsureSuccessWithDetailsAsync(cancellationToken);
    }

    public async Task RestoreUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/admin/users/{userId}/restore", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LoginMethodsDto> GetLoginMethodsAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<LoginMethodsDto>("api/users/me/login-methods", _serializerOptions, cancellationToken);
        return result!;
    }

    public async Task UnlinkExternalLoginAsync(string provider, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/users/me/login-methods/{Uri.EscapeDataString(provider)}", cancellationToken);
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

    public async Task<List<LiteMediaDto>> GetSelfMediaExclusionsAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<LiteMediaDto>>("api/users/me/media-exclusions", _serializerOptions, cancellationToken);
        return result ?? [];
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

    public async Task<PaginatedListDto<BackgroundTaskDto>> GetBackgroundTasksAsync(int pageNumber = 1, int pageSize = 20, IReadOnlyCollection<BackgroundTaskStatus>? statuses = null, IReadOnlyCollection<string>? names = null, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default)
    {
        var uri = $"api/background-tasks?pageNumber={pageNumber}&pageSize={pageSize}";
        if (statuses is { Count: > 0 })
        {
            foreach (var status in statuses)
            {
                uri += $"&status={status}";
            }
        }
        if (names is { Count: > 0 })
        {
            foreach (var name in names)
            {
                uri += $"&names={Uri.EscapeDataString(name)}";
            }
        }
        if (sortBy is not null)
        {
            uri += $"&sortBy={Uri.EscapeDataString(sortBy)}&sortDescending={sortDescending}";
        }
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<BackgroundTaskDto>>(uri, _serializerOptions, cancellationToken) ?? new PaginatedListDto<BackgroundTaskDto>();
    }

    public async Task<BackgroundTaskDto> GetBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<BackgroundTaskDto>($"api/background-tasks/{id}", _serializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Task not found");
    }

    public async Task DeleteBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/background-tasks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelBackgroundTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/background-tasks/{id}/cancel", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<BackgroundTaskSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<BackgroundTaskSettingsDto>("api/admin/background-tasks/settings", _serializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Settings not found");
    }

    public async Task UpdateSettingsAsync(UpdateBackgroundTaskSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/admin/background-tasks/settings", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<BackgroundTaskSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<BackgroundTaskSummaryDto>("api/background-tasks/summary", _serializerOptions, cancellationToken)
            ?? new BackgroundTaskSummaryDto { TotalCount = 0, StatusCounts = [], TaskTypeCounts = [] };
    }

    public async Task<List<LibraryHealthSummaryDto>> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<LibraryHealthSummaryDto>>("api/diagnostics/summary", _serializerOptions, cancellationToken) ?? [];
    }

    public async Task<PaginatedListDto<DiagnosticItemDto>> GetDiagnosticItemsAsync(Guid? libraryId = null, DiagnosticEntityType? entityType = null, DiagnosticIssue? issue = null, IReadOnlyCollection<DiagnosticIssue>? issues = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var uri = $"api/diagnostics/items?pageNumber={pageNumber}&pageSize={pageSize}";
        if (libraryId.HasValue)
            uri += $"&libraryId={libraryId.Value}";
        if (entityType.HasValue)
            uri += $"&entityType={entityType.Value}";
        if (issue.HasValue)
            uri += $"&issue={issue.Value}";
        if (issues is { Count: > 0 })
            foreach (var i in issues)
                uri += $"&issue={i}";
        return await HttpClient.GetFromJsonAsync<PaginatedListDto<DiagnosticItemDto>>(uri, _serializerOptions, cancellationToken) ?? new PaginatedListDto<DiagnosticItemDto>();
    }

    public async Task<int> FixDiagnosticItemsAsync(IReadOnlyList<Guid> entityIds, DiagnosticFixAction action, CancellationToken cancellationToken = default)
    {
        var request = new FixDiagnosticItemsRequest { EntityIds = entityIds, Action = action };
        var response = await HttpClient.PostAsJsonAsync("api/diagnostics/fix", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(_serializerOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetSelfLibraryExclusionsAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<Guid>>("api/users/me/preferences/library-exclusions", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task UpdateSelfLibraryExclusionsAsync(UpdateSelfLibraryExclusionsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/preferences/library-exclusions", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<HomeLayoutDto> GetHomeLayoutAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<HomeLayoutDto>("api/users/me/preferences/home-layout", _serializerOptions, cancellationToken);
        return result!;
    }

    public async Task UpdateHomeLayoutAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/preferences/home-layout", layout, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetHomeLayoutAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync("api/users/me/preferences/home-layout", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<HomeLayoutDto?> GetServerHomeLayoutAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync("api/server/preferences/home-layout", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<HomeLayoutDto>(_serializerOptions, cancellationToken);
    }

    public async Task<HomeLayoutDto> GetEffectiveServerHomeLayoutAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<HomeLayoutDto>("api/server/preferences/home-layout/effective", _serializerOptions, cancellationToken);
        return result ?? new HomeLayoutDto { Rows = [] };
    }

    public async Task UpdateServerHomeLayoutAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/server/preferences/home-layout", layout, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteServerHomeLayoutAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync("api/server/preferences/home-layout", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<MediaSegmentDto>> GetMediaSegmentsAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<MediaSegmentDto>>($"api/medias/{mediaId}/segments", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task DetectMediaSegmentsAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/medias/{seasonId}/detect-segments", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<LiteMediaDto>> GetSimilarMediaAsync(Guid mediaId, CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<LiteMediaDto>>($"api/medias/{mediaId}/similar", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<IReadOnlyList<LiteMusicTrackDto>> GetArtistTopTracksAsync(Guid artistId, CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<LiteMusicTrackDto>>($"api/medias/{artistId}/top-tracks", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<IReadOnlyList<PlayedMusicTrackDto>> GetTopMusicTracksAsync(Guid[]? libraryIds = null, int count = 20, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { $"count={count}" };
        if (libraryIds is { Length: > 0 })
            query.AddRange(libraryIds.Select(id => $"libraryIds={id}"));

        var uri = $"api/music/top-tracks?{string.Join('&', query)}";
        var result = await HttpClient.GetFromJsonAsync<List<PlayedMusicTrackDto>>(uri, _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<List<PersonKnownForItemDto>> GetPersonKnownForAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<List<PersonKnownForItemDto>>($"api/persons/{personId}/known-for", _serializerOptions, cancellationToken);
        return result ?? [];
    }

    public async Task<ServerFeatureFlagsDto> GetServerFeatureFlagsAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<ServerFeatureFlagsDto>("api/server/preferences/feature-flags", _serializerOptions, cancellationToken);
        return result ?? new ServerFeatureFlagsDto();
    }

    public async Task UpdateServerFeatureFlagsAsync(ServerFeatureFlagsDto flags, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/server/preferences/feature-flags", flags, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // IFederationService

    public async Task<List<PeerServerDto>> GetPeerServersAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<PeerServerDto>>("api/federation/peers", _serializerOptions, cancellationToken) ?? [];
    }

    public async Task<List<PeerRequestDto>> GetPeerRequestsAsync(CancellationToken cancellationToken = default)
    {
        return await HttpClient.GetFromJsonAsync<List<PeerRequestDto>>("api/federation/peers/requests", _serializerOptions, cancellationToken) ?? [];
    }

    public async Task RequestPeerAsync(string remoteUrl, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/federation/peers/request", new { remoteUrl }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AcceptPeerAsync(Guid requestId, IReadOnlyList<Guid> sharedLibraryIds, bool autoShareNewLibraries = false, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync($"api/federation/peers/requests/{requestId}/accept", new { SharedLibraryIds = sharedLibraryIds, AutoShareNewLibraries = autoShareNewLibraries }, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectPeerAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/federation/peers/requests/{requestId}/reject", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePeerAsync(Guid peerId, UpdatePeerRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/federation/peers/{peerId}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> TestPeerAsync(Guid peerId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/federation/peers/{peerId}/test", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<TestPeerResponse>(_serializerOptions, cancellationToken);
        return result?.Reachable ?? false;
    }

    public async Task RevokePeerAsync(Guid peerId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/federation/peers/{peerId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SyncPeerAsync(Guid peerId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/federation/peers/{peerId}/sync", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<PeerShareAgreementDto>> DiscoverPeerLibrariesAsync(Guid peerId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/federation/peers/{peerId}/discover-libraries", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<PeerShareAgreementDto>>(_serializerOptions, cancellationToken) ?? [];
    }

    public async Task<IndexedFileDto?> GetRemoteFileDetailsAsync(Guid remoteFileId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync($"api/remote-indexed-files/{remoteFileId}/details", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<IndexedFileDto>(_serializerOptions, cancellationToken);
    }

    public async Task<VideoPlayerSettingsDto?> GetServerVideoPlayerSettingsAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetAsync("api/server/preferences/video-player", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VideoPlayerSettingsDto>(_serializerOptions, cancellationToken);
    }

    public async Task UpdateServerVideoPlayerSettingsAsync(VideoPlayerSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/server/preferences/video-player", settings, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteServerVideoPlayerSettingsAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync("api/server/preferences/video-player", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<VideoPlayerSettingsDto> GetEffectiveVideoPlayerSettingsAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<VideoPlayerSettingsDto>("api/users/me/preferences/video-player", _serializerOptions, cancellationToken);
        return result ?? new VideoPlayerSettingsDto();
    }

    public async Task UpdateUserVideoPlayerSettingsAsync(VideoPlayerSettingsDto settings, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/preferences/video-player", settings, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetUserVideoPlayerSettingsAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync("api/users/me/preferences/video-player", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TrackSelectionPreferencesDto> GetEffectiveTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default)
    {
        var url = libraryId.HasValue
            ? $"api/users/me/preferences/track-selection?libraryId={libraryId.Value}"
            : "api/users/me/preferences/track-selection";
        var result = await HttpClient.GetFromJsonAsync<TrackSelectionPreferencesDto>(url, _serializerOptions, cancellationToken);
        return result ?? new TrackSelectionPreferencesDto();
    }

    public async Task UpdateUserTrackSelectionPreferencesAsync(TrackSelectionPreferencesDto preferences, Guid? libraryId = null, CancellationToken cancellationToken = default)
    {
        var url = libraryId.HasValue
            ? $"api/users/me/preferences/track-selection?libraryId={libraryId.Value}"
            : "api/users/me/preferences/track-selection";
        var response = await HttpClient.PutAsJsonAsync(url, preferences, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetUserTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default)
    {
        var url = libraryId.HasValue
            ? $"api/users/me/preferences/track-selection?libraryId={libraryId.Value}"
            : "api/users/me/preferences/track-selection";
        var response = await HttpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<SyncPlayPreferencesDto> GetSyncPlayPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var result = await HttpClient.GetFromJsonAsync<SyncPlayPreferencesDto>("api/users/me/preferences/syncplay", _serializerOptions, cancellationToken);
        return result ?? new SyncPlayPreferencesDto();
    }

    public async Task UpdateSyncPlayPreferencesAsync(SyncPlayPreferencesDto preferences, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("api/users/me/preferences/syncplay", preferences, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TrackSelectionPreferencesDto?> GetServerTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default)
    {
        var url = libraryId.HasValue
            ? $"api/server/preferences/track-selection?libraryId={libraryId.Value}"
            : "api/server/preferences/track-selection";
        var response = await HttpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TrackSelectionPreferencesDto>(_serializerOptions, cancellationToken);
    }

    public async Task UpdateServerTrackSelectionPreferencesAsync(TrackSelectionPreferencesDto preferences, Guid? libraryId = null, CancellationToken cancellationToken = default)
    {
        var url = libraryId.HasValue
            ? $"api/server/preferences/track-selection?libraryId={libraryId.Value}"
            : "api/server/preferences/track-selection";
        var response = await HttpClient.PutAsJsonAsync(url, preferences, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteServerTrackSelectionPreferencesAsync(Guid? libraryId = null, CancellationToken cancellationToken = default)
    {
        var url = libraryId.HasValue
            ? $"api/server/preferences/track-selection?libraryId={libraryId.Value}"
            : "api/server/preferences/track-selection";
        var response = await HttpClient.DeleteAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // IDownloadService

    public async Task<DownloadDto> PrepareDownloadAsync(PrepareDownloadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/downloads/prepare", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DownloadDto>(_serializerOptions, cancellationToken))!;
    }

    public async Task<DownloadDto> GetDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        return (await HttpClient.GetFromJsonAsync<DownloadDto>($"api/downloads/{downloadId}", _serializerOptions, cancellationToken))!;
    }

    public async Task DeleteDownloadAsync(Guid downloadId, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/downloads/{downloadId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public string GetDownloadFileUrl(Guid downloadId)
    {
        return $"api/downloads/{downloadId}/file";
    }

    public async Task<List<NotificationRuleDto>> GetNotificationRulesAsync(CancellationToken cancellationToken = default)
    {
        return (await HttpClient.GetFromJsonAsync<List<NotificationRuleDto>>("api/notifications/rules", _serializerOptions, cancellationToken))!;
    }

    public async Task<NotificationRuleDto> GetNotificationRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return (await HttpClient.GetFromJsonAsync<NotificationRuleDto>($"api/notifications/rules/{id}", _serializerOptions, cancellationToken))!;
    }

    public async Task<Guid> CreateNotificationRuleAsync(CreateNotificationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("api/notifications/rules", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(_serializerOptions, cancellationToken);
    }

    public async Task UpdateNotificationRuleAsync(Guid id, UpdateNotificationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync($"api/notifications/rules/{id}", request, _serializerOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteNotificationRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.DeleteAsync($"api/notifications/rules/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TestNotificationRuleResponse> TestNotificationRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"api/notifications/rules/{id}/test", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TestNotificationRuleResponse>(_serializerOptions, cancellationToken))!;
    }

    public async Task<List<NotificationEventDescriptorDto>> GetAvailableEventsAsync(CancellationToken cancellationToken = default)
    {
        return (await HttpClient.GetFromJsonAsync<List<NotificationEventDescriptorDto>>("api/notifications/events", _serializerOptions, cancellationToken))!;
    }

    private sealed record EphemeralTokenResponse(string Token);
    private sealed record TestPeerResponse(bool Reachable);
}
