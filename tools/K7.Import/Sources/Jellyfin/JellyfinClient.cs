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

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var items = new List<SourceMediaItem>();
        var startIndex = 0;
        const int pageSize = 100;
        var totalCount = 0;

        progress?.Report("page 1...");

        while (true)
        {
            var page = (startIndex / pageSize) + 1;
            if (totalCount > 0)
            {
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                progress?.Report($"page {page}/{totalPages}...");
            }
            else if (page > 1)
            {
                progress?.Report($"page {page}...");
            }

            var url = $"/Users/{userId}/Items?ParentId={libraryId}&Recursive=true&Fields=ProviderIds,UserData,Path&StartIndex={startIndex}&Limit={pageSize}&IncludeItemTypes=Movie,Episode,Audio";
            var response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            if (!response.TryGetProperty("Items", out var itemsArr))
                break;

            foreach (var item in itemsArr.EnumerateArray())
            {
                items.Add(ParseMediaItem(item));
            }

            totalCount = response.GetProperty("TotalRecordCount").GetInt32();
            var totalPagesDone = Math.Max(1, (int)Math.Ceiling(Math.Max(totalCount, 1) / (double)pageSize));
            progress?.Report(
                $"page {page}/{totalPagesDone} ({Math.Min(startIndex + itemsArr.GetArrayLength(), totalCount)}/{totalCount} items)");

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

        if (response.TryGetProperty("Items", out var playlistsArr))
        {
            foreach (var pl in playlistsArr.EnumerateArray())
            {
                var playlistId = pl.GetProperty("Id").GetString()!;
                var title = pl.GetProperty("Name").GetString()!;
                var mediaType = pl.TryGetProperty("MediaType", out var mt) ? mt.GetString() : null;

                var itemsResponse = await _httpClient.GetFromJsonAsync<JsonElement>(
                    $"/Playlists/{playlistId}/Items?UserId={userId}&Fields=ProviderIds,Path,AlbumArtist,Album,ProductionYear,Artists", cancellationToken);

                var playlistItems = new List<SourcePlaylistItem>();
                if (itemsResponse.TryGetProperty("Items", out var itemsArr))
                {
                    foreach (var item in itemsArr.EnumerateArray())
                    {
                        playlistItems.Add(ToPlaylistItem(item));
                    }
                }

                playlists.Add(new SourcePlaylist
                {
                    Id = playlistId,
                    Title = title,
                    MediaType = mediaType switch
                    {
                        "Audio" => "music",
                        "Video" => "video",
                        _ => null
                    },
                    Items = playlistItems
                });
            }
        }

        // Jellyfin has no built-in Liked playlist: hearts live in UserData.IsFavorite.
        // Plugins often materialize an empty/sync "Liked Songs" playlist - prefer filling from favorites.
        var likedSongs = await GetFavoriteItemsAsPlaylistAsync(
            userId,
            includeItemTypes: "Audio",
            playlistId: "liked-songs",
            title: "Liked Songs",
            mediaType: "music",
            cancellationToken);

        if (likedSongs is not null)
        {
            var existingLiked = playlists.FirstOrDefault(p => IsLikedSongsPlaylistName(p.Title));
            if (existingLiked is not null && existingLiked.Items.Count == 0)
            {
                playlists.Remove(existingLiked);
                playlists.Add(likedSongs with { Id = existingLiked.Id, Title = existingLiked.Title });
            }
            else if (existingLiked is null)
            {
                playlists.Add(likedSongs);
            }
            // If a real Liked playlist already has items, keep it as-is.
        }

        var videoFavorites = await GetFavoriteItemsAsPlaylistAsync(
            userId,
            includeItemTypes: "Movie,Episode",
            playlistId: "favorites-video",
            title: "Favoris",
            mediaType: "video",
            cancellationToken);

        if (videoFavorites is not null
            && !playlists.Any(p => string.Equals(p.Title, videoFavorites.Title, StringComparison.OrdinalIgnoreCase)
                                   && p.Items.Count > 0))
        {
            playlists.Add(videoFavorites);
        }

        return playlists;
    }

    private async Task<SourcePlaylist?> GetFavoriteItemsAsPlaylistAsync(
        string userId,
        string includeItemTypes,
        string playlistId,
        string title,
        string mediaType,
        CancellationToken cancellationToken)
    {
        var playlistItems = new List<SourcePlaylistItem>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Try several query shapes: Jellyfin versions differ on Filters vs isFavorite, and API keys
        // sometimes need the explicit boolean flag.
        var queries = new[]
        {
            $"/Users/{userId}/Items?isFavorite=true&Recursive=true&Fields=ProviderIds,Path,AlbumArtist,Album,ProductionYear,Artists&StartIndex={{0}}&Limit={{1}}&IncludeItemTypes={includeItemTypes}",
            $"/Users/{userId}/Items?Filters=IsFavorite&Recursive=true&Fields=ProviderIds,Path,AlbumArtist,Album,ProductionYear,Artists&StartIndex={{0}}&Limit={{1}}&IncludeItemTypes={includeItemTypes}",
            $"/Items?UserId={userId}&isFavorite=true&Recursive=true&Fields=ProviderIds,Path,AlbumArtist,Album,ProductionYear,Artists&StartIndex={{0}}&Limit={{1}}&IncludeItemTypes={includeItemTypes}"
        };

        foreach (var queryTemplate in queries)
        {
            var startIndex = 0;
            const int pageSize = 100;
            var gotAny = false;

            while (true)
            {
                var url = string.Format(System.Globalization.CultureInfo.InvariantCulture, queryTemplate, startIndex, pageSize);
                JsonElement response;
                try
                {
                    response = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);
                }
                catch (HttpRequestException)
                {
                    break;
                }

                if (!response.TryGetProperty("Items", out var itemsArr) || itemsArr.GetArrayLength() == 0)
                    break;

                gotAny = true;
                foreach (var item in itemsArr.EnumerateArray())
                {
                    var id = item.GetProperty("Id").GetString()!;
                    if (!seenIds.Add(id))
                        continue;

                    playlistItems.Add(ToPlaylistItem(item));
                }

                var totalCount = response.TryGetProperty("TotalRecordCount", out var total) && total.ValueKind == JsonValueKind.Number
                    ? total.GetInt32()
                    : 0;
                startIndex += pageSize;
                if (startIndex >= totalCount)
                    break;
            }

            // One successful query shape is enough; merge across shapes only if the first returned nothing.
            if (gotAny)
                break;
        }

        // Thumbs-up "Likes" (distinct from heart favorites) - Audio only, fold into Liked Songs.
        if (includeItemTypes.Contains("Audio", StringComparison.OrdinalIgnoreCase))
        {
            await AppendFilterMatchesAsync(
                userId,
                "Likes",
                "Audio",
                playlistItems,
                seenIds,
                cancellationToken);
        }

        if (playlistItems.Count == 0)
            return null;

        return new SourcePlaylist
        {
            Id = playlistId,
            Title = title,
            MediaType = mediaType,
            Items = playlistItems
        };
    }

    private async Task AppendFilterMatchesAsync(
        string userId,
        string filter,
        string includeItemTypes,
        List<SourcePlaylistItem> playlistItems,
        HashSet<string> seenIds,
        CancellationToken cancellationToken)
    {
        var startIndex = 0;
        const int pageSize = 100;

        while (true)
        {
            JsonElement response;
            try
            {
                response = await _httpClient.GetFromJsonAsync<JsonElement>(
                    $"/Users/{userId}/Items?Filters={filter}&Recursive=true&Fields=ProviderIds,Path,AlbumArtist,Album,ProductionYear,Artists&StartIndex={startIndex}&Limit={pageSize}&IncludeItemTypes={includeItemTypes}",
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return;
            }

            if (!response.TryGetProperty("Items", out var itemsArr) || itemsArr.GetArrayLength() == 0)
                break;

            foreach (var item in itemsArr.EnumerateArray())
            {
                var id = item.GetProperty("Id").GetString()!;
                if (!seenIds.Add(id))
                    continue;

                playlistItems.Add(ToPlaylistItem(item));
            }

            var totalCount = response.TryGetProperty("TotalRecordCount", out var total) && total.ValueKind == JsonValueKind.Number
                ? total.GetInt32()
                : 0;
            startIndex += pageSize;
            if (startIndex >= totalCount)
                break;
        }
    }

    private static bool IsLikedSongsPlaylistName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var normalized = title.Trim().ToLowerInvariant();
        return normalized is "liked songs" or "liked" or "favorited songs" or "favourite songs" or "favorite songs"
            or "titres likés" or "titres likes" or "chansons aimées" or "chansons aimees" or "favoris music"
            or "mes titres likés" or "mes titres likes";
    }

    private static SourcePlaylistItem ToPlaylistItem(JsonElement item)
    {
        var itemType = item.TryGetProperty("Type", out var itp) ? itp.GetString() : null;
        var filePaths = new List<string>();
        if (item.TryGetProperty("Path", out var path) && path.ValueKind == JsonValueKind.String)
        {
            var pathValue = path.GetString();
            if (!string.IsNullOrWhiteSpace(pathValue))
                filePaths.Add(pathValue.Replace('\\', '/'));
        }

        string? artistName = null;
        if (itemType == "Audio")
        {
            if (item.TryGetProperty("AlbumArtist", out var albumArtist) && albumArtist.ValueKind == JsonValueKind.String)
                artistName = albumArtist.GetString();
            else if (item.TryGetProperty("Artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
                artistName = artists.EnumerateArray().Select(a => a.GetString()).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
        }

        return new SourcePlaylistItem
        {
            Id = item.GetProperty("Id").GetString()!,
            Title = item.GetProperty("Name").GetString()!,
            ProviderIds = ParseProviderIds(item, itemType),
            FilePaths = filePaths,
            MediaType = itemType switch
            {
                "Movie" => "movie",
                "Episode" => "episode",
                "Audio" => "music",
                "Series" => "serie",
                _ => null
            },
            ArtistName = artistName,
            AlbumName = itemType == "Audio" && item.TryGetProperty("Album", out var album) && album.ValueKind == JsonValueKind.String
                ? album.GetString()
                : null,
            Year = item.TryGetProperty("ProductionYear", out var year) && year.ValueKind == JsonValueKind.Number
                ? year.GetInt32()
                : null,
            SeriesTitle = itemType == "Episode" && item.TryGetProperty("SeriesName", out var seriesName) && seriesName.ValueKind == JsonValueKind.String
                ? seriesName.GetString()
                : null,
            SeasonNumber = itemType == "Episode" && item.TryGetProperty("ParentIndexNumber", out var parentIndex) && parentIndex.ValueKind == JsonValueKind.Number
                ? parentIndex.GetInt32()
                : null,
            EpisodeNumber = itemType == "Episode" && item.TryGetProperty("IndexNumber", out var index) && index.ValueKind == JsonValueKind.Number
                ? index.GetInt32()
                : null
        };
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
            rating = ratingVal.ValueKind switch
            {
                JsonValueKind.True => 10.0,
                JsonValueKind.False => 1.0,
                JsonValueKind.Number => Math.Clamp(ratingVal.GetDouble(), 0, 10),
                _ => null
            };
        }

        var runTimeTicks = item.TryGetProperty("RunTimeTicks", out var rtt) ? rtt.GetInt64() : 0L;
        var durationSeconds = runTimeTicks > 0 ? runTimeTicks / 10_000_000.0 : (double?)null;

        var jellyfinType = item.TryGetProperty("Type", out var tp) ? tp.GetString() : null;

        var filePaths = new List<string>();
        if (item.TryGetProperty("Path", out var path) && path.ValueKind == JsonValueKind.String)
        {
            var pathValue = path.GetString();
            if (!string.IsNullOrWhiteSpace(pathValue))
                filePaths.Add(pathValue.Replace('\\', '/'));
        }

        return new SourceMediaItem
        {
            Id = item.GetProperty("Id").GetString()!,
            Title = item.GetProperty("Name").GetString()!,
            Year = item.TryGetProperty("ProductionYear", out var year) && year.ValueKind == JsonValueKind.Number ? year.GetInt32() : null,
            ProviderIds = ParseProviderIds(item, jellyfinType),
            FilePaths = filePaths,
            PlayCount = playCount,
            LastPlaybackPosition = positionTicks / 10_000_000.0,
            DurationSeconds = durationSeconds,
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
