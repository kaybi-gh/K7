using System.Net.Http.Json;
using System.Text.Json;
using K7.Import.Models;

namespace K7.Import.Sources.Jellyfin;

public sealed class JellyfinClient : ISourceClient
{
    private readonly HttpClient _httpClient;

    public JellyfinClient(string serverUrl, string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/'))
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"MediaBrowser Token=\"{apiKey}\"");
    }

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var info = await _httpClient.GetFromJsonAsync<JsonElement>("/System/Info", cancellationToken);
        return new SourceServerInfo
        {
            Name = info.GetProperty("ServerName").GetString() ?? "Jellyfin",
            Version = info.GetProperty("Version").GetString()
        };
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var usersJson = await _httpClient.GetFromJsonAsync<JsonElement>("/Users", cancellationToken);
        var users = new List<SourceUser>();

        foreach (var user in usersJson.EnumerateArray())
        {
            users.Add(new SourceUser
            {
                Id = user.GetProperty("Id").GetString()!,
                Name = user.GetProperty("Name").GetString()!
            });
        }

        return users;
    }

    public async Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var foldersJson = await _httpClient.GetFromJsonAsync<JsonElement>("/Library/VirtualFolders", cancellationToken);
        var libraries = new List<SourceLibrary>();

        foreach (var folder in foldersJson.EnumerateArray())
        {
            libraries.Add(new SourceLibrary
            {
                Id = folder.GetProperty("ItemId").GetString()!,
                Name = folder.GetProperty("Name").GetString()!,
                MediaType = folder.TryGetProperty("CollectionType", out var ct) ? ct.GetString() : null
            });
        }

        return libraries;
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, CancellationToken cancellationToken = default)
    {
        var items = new List<SourceMediaItem>();
        var startIndex = 0;
        const int pageSize = 100;

        while (true)
        {
            var url = $"/Users/{userId}/Items?ParentId={libraryId}&Recursive=true&Fields=ProviderIds&StartIndex={startIndex}&Limit={pageSize}&IncludeItemTypes=Movie,Episode,Audio";
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            if (!response.TryGetProperty("Items", out var itemsArr))
                break;

            foreach (var item in itemsArr.EnumerateArray())
            {
                items.Add(ParseMediaItem(item));
            }

            var totalCount = response.GetProperty("TotalRecordCount").GetInt32();
            startIndex += pageSize;
            if (startIndex >= totalCount)
                break;
        }

        return items;
    }

    public async Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var playlists = new List<SourcePlaylist>();

        var response = await _httpClient.GetFromJsonAsync<JsonElement>(
            $"/Users/{userId}/Items?IncludeItemTypes=Playlist&Recursive=true", cancellationToken);

        if (!response.TryGetProperty("Items", out var playlistsArr))
            return playlists;

        foreach (var pl in playlistsArr.EnumerateArray())
        {
            var playlistId = pl.GetProperty("Id").GetString()!;
            var title = pl.GetProperty("Name").GetString()!;

            var itemsResponse = await _httpClient.GetFromJsonAsync<JsonElement>(
                $"/Playlists/{playlistId}/Items?UserId={userId}&Fields=ProviderIds", cancellationToken);

            var playlistItems = new List<SourcePlaylistItem>();
            if (itemsResponse.TryGetProperty("Items", out var itemsArr))
            {
                foreach (var item in itemsArr.EnumerateArray())
                {
                    playlistItems.Add(new SourcePlaylistItem
                    {
                        Id = item.GetProperty("Id").GetString()!,
                        Title = item.GetProperty("Name").GetString()!,
                        ProviderIds = ParseProviderIds(item)
                    });
                }
            }

            playlists.Add(new SourcePlaylist
            {
                Id = playlistId,
                Title = title,
                Items = playlistItems
            });
        }

        return playlists;
    }

    private static SourceMediaItem ParseMediaItem(JsonElement item)
    {
        var userData = item.TryGetProperty("UserData", out var ud) ? ud : (JsonElement?)null;
        var playCount = userData?.TryGetProperty("PlayCount", out var pc) == true ? pc.GetInt32() : 0;
        var lastPlayedDate = userData?.TryGetProperty("LastPlayedDate", out var lpd) == true && lpd.ValueKind != JsonValueKind.Null
            ? lpd.GetDateTime()
            : (DateTime?)null;
        var positionTicks = userData?.TryGetProperty("PlaybackPositionTicks", out var ppt) == true ? ppt.GetInt64() : 0L;
        var played = userData?.TryGetProperty("Played", out var pl) == true && pl.GetBoolean();

        double? rating = null;
        if (userData?.TryGetProperty("Rating", out var ratingVal) == true && ratingVal.ValueKind != JsonValueKind.Null)
        {
            // Jellyfin uses boolean like/dislike
            rating = ratingVal.ValueKind == JsonValueKind.True ? 10.0
                   : ratingVal.ValueKind == JsonValueKind.False ? 1.0
                   : null;
        }

        var jellyfinType = item.TryGetProperty("Type", out var tp) ? tp.GetString() : null;

        return new SourceMediaItem
        {
            Id = item.GetProperty("Id").GetString()!,
            Title = item.GetProperty("Name").GetString()!,
            Year = item.TryGetProperty("ProductionYear", out var year) && year.ValueKind == JsonValueKind.Number ? year.GetInt32() : null,
            ProviderIds = ParseProviderIds(item, jellyfinType),
            PlayCount = playCount,
            LastPlaybackPosition = positionTicks / 10_000_000.0,
            LastPlayedAt = lastPlayedDate,
            IsCompleted = played,
            Rating = rating,
            MediaType = jellyfinType switch
            {
                "Movie" => "movie",
                "Episode" => "episode",
                "Audio" => "music",
                _ => jellyfinType?.ToLowerInvariant()
            },
            ArtistName = jellyfinType == "Audio" && item.TryGetProperty("AlbumArtist", out var albumArtist) ? albumArtist.GetString() : null,
            AlbumName = jellyfinType == "Audio" && item.TryGetProperty("Album", out var album) ? album.GetString() : null,
            SeriesTitle = jellyfinType == "Episode" && item.TryGetProperty("SeriesName", out var seriesName) ? seriesName.GetString() : null,
            SeasonNumber = jellyfinType == "Episode" && item.TryGetProperty("ParentIndexNumber", out var parentIndex) && parentIndex.ValueKind == JsonValueKind.Number ? parentIndex.GetInt32() : null,
            EpisodeNumber = jellyfinType == "Episode" && item.TryGetProperty("IndexNumber", out var index) && index.ValueKind == JsonValueKind.Number ? index.GetInt32() : null
        };
    }

    private static Dictionary<string, string> ParseProviderIds(JsonElement item, string? jellyfinType = null)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (item.TryGetProperty("ProviderIds", out var providers) && providers.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in providers.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var key = prop.Name.ToLowerInvariant() switch
                    {
                        "tmdb" => "tmdb",
                        "imdb" => "imdb",
                        "tvdb" => "tvdb",
                        "musicbrainztrack" when jellyfinType is "Audio" => "musicbrainz",
                        "musicbrainzalbum" when jellyfinType is "MusicAlbum" => "musicbrainz",
                        "musicbrainzreleasegroup" when jellyfinType is "MusicAlbum" => "musicbrainz",
                        "musicbrainzartist" when jellyfinType is "MusicArtist" => "musicbrainz",
                        "musicbrainztrack" or "musicbrainzalbum" or "musicbrainzartist" or "musicbrainzreleasegroup" => null,
                        _ => prop.Name.ToLowerInvariant()
                    };
                    if (key is not null)
                        providerIds.TryAdd(key, prop.Value.GetString()!);
                }
            }
        }

        return providerIds;
    }
}
