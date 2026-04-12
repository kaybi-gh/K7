using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using K7.Import.Models;

namespace K7.Import.Sources.Plex;

public sealed partial class PlexClient : ISourceClient
{
    private readonly HttpClient _httpClient;

    public PlexClient(string serverUrl, string token)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/'))
        };
        _httpClient.DefaultRequestHeaders.Add("X-Plex-Token", token);
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/", cancellationToken);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var container = doc.GetProperty("MediaContainer");
        return new SourceServerInfo
        {
            Name = container.GetProperty("friendlyName").GetString() ?? "Plex",
            Version = container.GetProperty("version").GetString()
        };
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = new List<SourceUser>();

        var identityResponse = await _httpClient.GetAsync("/", cancellationToken);
        identityResponse.EnsureSuccessStatusCode();
        var identityDoc = await identityResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var container = identityDoc.GetProperty("MediaContainer");
        if (container.TryGetProperty("myPlexUsername", out var ownerName))
        {
            users.Add(new SourceUser
            {
                Id = "owner",
                Name = ownerName.GetString() ?? "Owner"
            });
        }

        try
        {
            var accountsResponse = await _httpClient.GetAsync("/accounts", cancellationToken);
            if (accountsResponse.IsSuccessStatusCode)
            {
                var accountsDoc = await accountsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                if (accountsDoc.TryGetProperty("MediaContainer", out var mc) &&
                    mc.TryGetProperty("Account", out var accounts))
                {
                    foreach (var account in accounts.EnumerateArray())
                    {
                        var id = account.GetProperty("id").ToString();
                        var name = account.GetProperty("name").GetString() ?? id;
                        if (users.All(u => u.Name != name))
                        {
                            users.Add(new SourceUser { Id = id, Name = name });
                        }
                    }
                }
            }
        }
        catch
        {
            // /accounts may not be available on all setups
        }

        return users;
    }

    public async Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/library/sections", cancellationToken);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var directories = doc.GetProperty("MediaContainer").GetProperty("Directory");

        var libraries = new List<SourceLibrary>();
        foreach (var dir in directories.EnumerateArray())
        {
            libraries.Add(new SourceLibrary
            {
                Id = dir.GetProperty("key").GetString()!,
                Name = dir.GetProperty("title").GetString()!,
                MediaType = dir.GetProperty("type").GetString()
            });
        }
        return libraries;
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, CancellationToken cancellationToken = default)
    {
        var items = new List<SourceMediaItem>();
        var offset = 0;
        const int pageSize = 100;

        while (true)
        {
            var response = await _httpClient.GetAsync(
                $"/library/sections/{libraryId}/all?X-Plex-Container-Start={offset}&X-Plex-Container-Size={pageSize}&includeGuids=1",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var container = doc.GetProperty("MediaContainer");

            if (!container.TryGetProperty("Metadata", out var metadata))
                break;

            foreach (var item in metadata.EnumerateArray())
            {
                items.Add(ParseMediaItem(item));
            }

            var totalSize = container.GetProperty("totalSize").GetInt32();
            offset += pageSize;
            if (offset >= totalSize)
                break;
        }

        return items;
    }

    public async Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var playlists = new List<SourcePlaylist>();

        var response = await _httpClient.GetAsync("/playlists", cancellationToken);
        if (!response.IsSuccessStatusCode) return playlists;

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!doc.GetProperty("MediaContainer").TryGetProperty("Metadata", out var playlistsArr))
            return playlists;

        foreach (var pl in playlistsArr.EnumerateArray())
        {
            var ratingKey = pl.GetProperty("ratingKey").GetString()!;
            var title = pl.GetProperty("title").GetString()!;

            var itemsResponse = await _httpClient.GetAsync(
                $"/playlists/{ratingKey}/items?includeGuids=1", cancellationToken);
            if (!itemsResponse.IsSuccessStatusCode) continue;

            var itemsDoc = await itemsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var playlistItems = new List<SourcePlaylistItem>();

            if (itemsDoc.GetProperty("MediaContainer").TryGetProperty("Metadata", out var itemsArr))
            {
                foreach (var item in itemsArr.EnumerateArray())
                {
                    playlistItems.Add(new SourcePlaylistItem
                    {
                        Id = item.GetProperty("ratingKey").GetString()!,
                        Title = item.GetProperty("title").GetString()!,
                        ProviderIds = ParseGuids(item)
                    });
                }
            }

            playlists.Add(new SourcePlaylist
            {
                Id = ratingKey,
                Title = title,
                Items = playlistItems
            });
        }

        return playlists;
    }

    private static SourceMediaItem ParseMediaItem(JsonElement item)
    {
        var viewCount = item.TryGetProperty("viewCount", out var vc) ? vc.GetInt32() : 0;
        var viewOffset = item.TryGetProperty("viewOffset", out var vo) ? vo.GetInt64() / 1000.0 : (double?)null;
        var lastViewedAt = item.TryGetProperty("lastViewedAt", out var lva)
            ? DateTimeOffset.FromUnixTimeSeconds(lva.GetInt64()).UtcDateTime
            : (DateTime?)null;
        var userRating = item.TryGetProperty("userRating", out var ur) ? ur.GetDouble() : (double?)null;
        var year = item.TryGetProperty("year", out var y) ? y.GetInt32() : (int?)null;
        var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;

        return new SourceMediaItem
        {
            Id = item.GetProperty("ratingKey").GetString()!,
            Title = item.GetProperty("title").GetString()!,
            Year = year,
            ProviderIds = ParseGuids(item),
            PlayCount = viewCount,
            LastPlaybackPosition = viewOffset,
            LastPlayedAt = lastViewedAt,
            IsCompleted = viewCount > 0,
            Rating = userRating,
            MediaType = type switch
            {
                "movie" => "movie",
                "episode" => "episode",
                "track" => "music",
                _ => type
            },
            ArtistName = item.TryGetProperty("grandparentTitle", out var gpTitle) && type == "track" ? gpTitle.GetString() : null,
            AlbumName = item.TryGetProperty("parentTitle", out var pTitle) && type == "track" ? pTitle.GetString() : null,
            SeriesTitle = item.TryGetProperty("grandparentTitle", out var seriesTitle) && type == "episode" ? seriesTitle.GetString() : null,
            SeasonNumber = item.TryGetProperty("parentIndex", out var parentIdx) && type == "episode" ? parentIdx.GetInt32() : null,
            EpisodeNumber = item.TryGetProperty("index", out var idx) && type == "episode" ? idx.GetInt32() : null
        };
    }

    private static Dictionary<string, string> ParseGuids(JsonElement item)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (item.TryGetProperty("Guid", out var guids))
        {
            foreach (var guid in guids.EnumerateArray())
            {
                var id = guid.GetProperty("id").GetString();
                if (id is null) continue;

                var match = PlexGuidRegex().Match(id);
                if (match.Success)
                {
                    providerIds[match.Groups[1].Value.ToLowerInvariant()] = match.Groups[2].Value;
                }
            }
        }

        return providerIds;
    }

    [GeneratedRegex(@"^(\w+)://(.+)$")]
    private static partial Regex PlexGuidRegex();
}
