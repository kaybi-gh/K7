using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Responses;
using K7.Shared.Dtos.Users;

namespace K7.Import.Clients;

public sealed class K7ApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public K7ApiClient(string serverUrl, string accessToken)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/')),
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _httpClient.GetFromJsonAsync<List<UserDto>>("api/users", JsonOptions, cancellationToken);
        return users ?? [];
    }

    public async Task<UserDto> CreateUserAsync(string username, string role = "User", CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", new CreateUserRequest
        {
            Username = username,
            Role = role
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<List<ExternalIdMatchResult>> LookupMediasByExternalIdsAsync(
        IReadOnlyList<LookupMediasByExternalIdsRequest.ExternalIdItem> items,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/medias/by-external-ids",
            new LookupMediasByExternalIdsRequest { Items = items }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ExternalIdMatchResult>>(JsonOptions, cancellationToken)) ?? [];
    }

    public async Task<BulkCreateMediasResponse> BulkCreateMediasAsync(
        IReadOnlyList<BulkCreateMediasRequest.BulkCreateMediaItem> items,
        CancellationToken cancellationToken = default)
    {
        const int chunkSize = 200;
        var allResults = new List<BulkCreateMediasResponse.BulkCreateMediaResult>();

        foreach (var chunk in items.Chunk(chunkSize))
        {
            var response = await _httpClient.PostAsJsonAsync("api/medias/bulk-create",
                new BulkCreateMediasRequest { Items = chunk }, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<BulkCreateMediasResponse>(JsonOptions, cancellationToken);
            if (result?.Results is not null)
                allResults.AddRange(result.Results);
        }

        return new BulkCreateMediasResponse { Results = allResults };
    }

    public async Task<int> BulkLinkArtistsAsync(
        IReadOnlyList<BulkLinkArtistsRequest.ArtistLinkItem> items,
        CancellationToken cancellationToken = default)
    {
        const int chunkSize = 500;
        var total = 0;

        foreach (var chunk in items.Chunk(chunkSize))
        {
            var response = await _httpClient.PostAsJsonAsync("api/medias/bulk-link-artists",
                new BulkLinkArtistsRequest { Items = chunk }, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LinkResult>(JsonOptions, cancellationToken);
            total += result?.LinkedCount ?? 0;
        }

        return total;
    }

    public async Task<int> BulkUpsertMediaStatesAsync(Guid userId,
        IReadOnlyList<BulkUpsertMediaStatesRequest.MediaStateItem> items,
        MergeStrategy? strategy = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/media-states/bulk",
            new BulkUpsertMediaStatesRequest { Items = items, Strategy = strategy }, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpsertResult>(JsonOptions, cancellationToken);
        return result?.UpsertedCount ?? 0;
    }

    public async Task<int> BulkUpsertRatingsAsync(Guid userId,
        IReadOnlyList<BulkUpsertRatingsRequest.RatingItem> items,
        MergeStrategy? strategy = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/ratings/bulk",
            new BulkUpsertRatingsRequest { Items = items, Strategy = strategy }, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UpsertResult>(JsonOptions, cancellationToken);
        return result?.UpsertedCount ?? 0;
    }

    public async Task<int> BulkCreatePlaybackSessionsAsync(Guid userId,
        IReadOnlyList<BulkCreatePlaybackSessionsRequest.PlaybackSessionItem> items,
        CancellationToken cancellationToken = default)
    {
        const int chunkSize = 500;
        var total = 0;

        foreach (var chunk in items.Chunk(chunkSize))
        {
            var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/playback-sessions/bulk",
                new BulkCreatePlaybackSessionsRequest { Items = chunk }, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateResult>(JsonOptions, cancellationToken);
            total += result?.CreatedCount ?? 0;
        }

        return total;
    }

    public async Task<Guid> CreatePlaylistAsync(string title, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/playlists",
            new CreatePlaylistRequest { Title = title }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(JsonOptions, cancellationToken);
    }

    public async Task AddPlaylistItemAsync(Guid playlistId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/playlists/{playlistId}/items",
            new AddPlaylistItemRequest(mediaId), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record AddPlaylistItemRequest(Guid MediaId);

    private sealed record UpsertResult
    {
        public int UpsertedCount { get; init; }
    }

    private sealed record CreateResult
    {
        public int CreatedCount { get; init; }
    }

    private sealed record LinkResult
    {
        public int LinkedCount { get; init; }
    }
}
