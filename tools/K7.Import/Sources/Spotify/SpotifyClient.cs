using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using K7.Import.Models;

namespace K7.Import.Sources.Spotify;

public sealed class SpotifyClient : ISourceClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _dataDir;
    private readonly bool _hasApiToken;

    public SpotifyClient(string? accessToken, string? dataDir = null)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.spotify.com/")
        };
        _hasApiToken = !string.IsNullOrEmpty(accessToken);
        if (_hasApiToken)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _dataDir = dataDir;

        if (!_hasApiToken && (dataDir is null || !Directory.Exists(dataDir)))
            throw new ArgumentException("Spotify requires either --source-api-key (API token) or --spotify-data-dir (data export), or both.");
    }

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_hasApiToken)
        {
            var response = await _httpClient.GetAsync("v1/me", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        return new SourceServerInfo { Name = "Spotify", Version = null };
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        if (_hasApiToken)
        {
            var profile = await _httpClient.GetFromJsonAsync<JsonElement>("v1/me", cancellationToken);
            return
            [
                new SourceUser
                {
                    Id = profile.GetProperty("id").GetString()!,
                    Name = profile.TryGetProperty("display_name", out var dn) && dn.ValueKind == JsonValueKind.String
                        ? dn.GetString()!
                        : profile.GetProperty("id").GetString()!
                }
            ];
        }

        return [new SourceUser { Id = "local", Name = GetUsernameFromExport() ?? "spotify-user" }];
    }

    private string? GetUsernameFromExport()
    {
        if (_dataDir is null || !Directory.Exists(_dataDir)) return null;

        var file = Directory.GetFiles(_dataDir, "*.json").FirstOrDefault();

        if (file is null) return null;

        var json = File.ReadAllText(file);
        var entries = JsonSerializer.Deserialize<JsonElement>(json);

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
                return username.GetString();
        }

        return null;
    }

    public Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = new List<SourceLibrary>();

        if (_hasApiToken)
        {
            libraries.Add(new SourceLibrary { Id = "saved-tracks", Name = "Liked Songs", MediaType = "music" });
            libraries.Add(new SourceLibrary { Id = "saved-albums", Name = "Saved Albums", MediaType = "music" });
            libraries.Add(new SourceLibrary { Id = "recently-played", Name = "Recently Played (API, last 50)", MediaType = "music" });
        }

        if (_dataDir is not null && Directory.Exists(_dataDir))
        {
            libraries.Add(new SourceLibrary { Id = "streaming-history", Name = "Streaming History (export)", MediaType = "music" });
        }

        return Task.FromResult(libraries);
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, CancellationToken cancellationToken = default)
    {
        return libraryId switch
        {
            "saved-tracks" => await GetSavedTracksAsync(cancellationToken),
            "saved-albums" => await GetSavedAlbumTracksAsync(cancellationToken),
            "recently-played" => await GetRecentlyPlayedAsync(cancellationToken),
            "streaming-history" => LoadStreamingHistoryFromExport(),
            _ => []
        };
    }

    public async Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var playlists = new List<SourcePlaylist>();
        var url = "v1/me/playlists?limit=50";

        while (url is not null)
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            foreach (var pl in doc.GetProperty("items").EnumerateArray())
            {
                var playlistId = pl.GetProperty("id").GetString()!;
                var title = pl.GetProperty("name").GetString()!;

                var items = await GetPlaylistTracksAsync(playlistId, cancellationToken);
                playlists.Add(new SourcePlaylist
                {
                    Id = playlistId,
                    Title = title,
                    MediaType = "music",
                    Items = items
                });
            }

            url = doc.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return playlists;
    }

    private async Task<List<SourceMediaItem>> GetSavedTracksAsync(CancellationToken cancellationToken)
    {
        var items = new List<SourceMediaItem>();
        var url = "v1/me/tracks?limit=50";

        while (url is not null)
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            foreach (var entry in doc.GetProperty("items").EnumerateArray())
            {
                var track = entry.GetProperty("track");
                items.Add(ParseTrack(track, liked: true));
            }

            url = doc.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return items;
    }

    private async Task<List<SourceMediaItem>> GetSavedAlbumTracksAsync(CancellationToken cancellationToken)
    {
        var items = new List<SourceMediaItem>();
        var url = "v1/me/albums?limit=50";

        while (url is not null)
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            foreach (var entry in doc.GetProperty("items").EnumerateArray())
            {
                var album = entry.GetProperty("album");
                if (!album.TryGetProperty("tracks", out var tracks)) continue;

                foreach (var track in tracks.GetProperty("items").EnumerateArray())
                {
                    items.Add(ParseTrack(track, liked: false));
                }
            }

            url = doc.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return items;
    }

    private async Task<List<SourceMediaItem>> GetRecentlyPlayedAsync(CancellationToken cancellationToken)
    {
        var itemsByKey = new Dictionary<string, SourceMediaItem>();
        var url = "v1/me/player/recently-played?limit=50";

        var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

        foreach (var entry in doc.GetProperty("items").EnumerateArray())
        {
            var track = entry.GetProperty("track");
            var id = track.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                ? idProp.GetString()! : "";
            if (string.IsNullOrEmpty(id)) continue;

            var playedAt = entry.TryGetProperty("played_at", out var pa) && pa.ValueKind == JsonValueKind.String
                ? DateTime.Parse(pa.GetString()!).ToUniversalTime()
                : (DateTime?)null;

            if (itemsByKey.TryGetValue(id, out var existing))
            {
                itemsByKey[id] = existing with
                {
                    PlayCount = existing.PlayCount + 1,
                    LastPlayedAt = playedAt > existing.LastPlayedAt ? playedAt : existing.LastPlayedAt
                };
            }
            else
            {
                var name = track.TryGetProperty("name", out var nameProp) ? nameProp.GetString()! : "";
                var recentArtistName = track.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array
                    ? artists.EnumerateArray().FirstOrDefault().TryGetProperty("name", out var an) ? an.GetString() : null
                    : null;
                var recentAlbumName = track.TryGetProperty("album", out var albumEl) && albumEl.ValueKind == JsonValueKind.Object
                    ? albumEl.TryGetProperty("name", out var abn) ? abn.GetString() : null
                    : null;

                itemsByKey[id] = new SourceMediaItem
                {
                    Id = id,
                    Title = name,
                    ProviderIds = ParseTrackProviderIds(track),
                    PlayCount = 1,
                    IsCompleted = true,
                    LastPlayedAt = playedAt,
                    MediaType = "music",
                    ArtistName = recentArtistName,
                    AlbumName = recentAlbumName
                };
            }
        }

        return [.. itemsByKey.Values];
    }

    private List<SourceMediaItem> LoadStreamingHistoryFromExport()
    {
        if (_dataDir is null || !Directory.Exists(_dataDir))
            return [];

        var itemsByUri = new Dictionary<string, SourceMediaItem>(StringComparer.OrdinalIgnoreCase);

        // Load all JSON files and filter by content (support all Spotify export naming conventions)
        var files = Directory.GetFiles(_dataDir, "*.json");

        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            JsonElement entries;

            try
            {
                entries = JsonSerializer.Deserialize<JsonElement>(json);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entries.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var entry in entries.EnumerateArray())
            {
                var msPlayed = entry.TryGetProperty("ms_played", out var ms) ? ms.GetInt64()
                    : entry.TryGetProperty("msPlayed", out var msBasic) ? msBasic.GetInt64()
                    : 0;

                // Skip very short plays (< 30s)
                if (msPlayed < 30_000) continue;

                // Try extended format first (has spotify_track_uri)
                string? trackUri = null;
                string? trackName = null;
                string? artistName = null;
                DateTime? playedAt = null;

                if (entry.TryGetProperty("spotify_track_uri", out var uri) && uri.ValueKind == JsonValueKind.String)
                {
                    trackUri = uri.GetString();
                    trackName = entry.TryGetProperty("master_metadata_track_name", out var tn) && tn.ValueKind == JsonValueKind.String
                        ? tn.GetString() : null;
                    artistName = entry.TryGetProperty("master_metadata_album_artist_name", out var an) && an.ValueKind == JsonValueKind.String
                        ? an.GetString() : null;
                    playedAt = entry.TryGetProperty("ts", out var ts) && ts.ValueKind == JsonValueKind.String
                        ? DateTime.Parse(ts.GetString()!).ToUniversalTime()
                        : null;
                }
                else
                {
                    // Basic format: no track URI, use artist+track as key
                    trackName = entry.TryGetProperty("trackName", out var tn) ? tn.GetString() : null;
                    artistName = entry.TryGetProperty("artistName", out var an) ? an.GetString() : null;
                    playedAt = entry.TryGetProperty("endTime", out var et) && et.ValueKind == JsonValueKind.String
                        ? DateTime.Parse(et.GetString()!).ToUniversalTime()
                        : null;
                }

                if (trackName is null) continue;

                var albumName = entry.TryGetProperty("master_metadata_album_album_name", out var abn) && abn.ValueKind == JsonValueKind.String
                    ? abn.GetString() : null;

                // Extract Spotify ID from URI (spotify:track:ABC123)
                string? spotifyId = null;
                if (trackUri is not null && trackUri.StartsWith("spotify:track:"))
                    spotifyId = trackUri["spotify:track:".Length..];

                var key = spotifyId ?? $"{artistName}|{trackName}";

                var playEntry = playedAt.HasValue
                    ? new SourcePlayEntry { PlayedAt = playedAt.Value, DurationSeconds = msPlayed / 1000.0 }
                    : null;

                if (itemsByUri.TryGetValue(key, out var existing))
                {
                    if (playEntry is not null)
                        existing.PlayHistory.Add(playEntry);

                    itemsByUri[key] = existing with
                    {
                        PlayCount = existing.PlayCount + 1,
                        LastPlayedAt = playedAt > existing.LastPlayedAt ? playedAt : existing.LastPlayedAt
                    };
                }
                else
                {
                    var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (spotifyId is not null)
                        providerIds["spotify"] = spotifyId;

                    var title = trackName;
                    var history = new List<SourcePlayEntry>();
                    if (playEntry is not null)
                        history.Add(playEntry);

                    itemsByUri[key] = new SourceMediaItem
                    {
                        Id = key,
                        Title = title,
                        ProviderIds = providerIds,
                        PlayCount = 1,
                        IsCompleted = true,
                        LastPlayedAt = playedAt,
                        MediaType = "music",
                        ArtistName = artistName,
                        AlbumName = albumName,
                        PlayHistory = history
                    };
                }
            }
        }

        return [.. itemsByUri.Values];
    }

    private async Task<List<SourcePlaylistItem>> GetPlaylistTracksAsync(string playlistId, CancellationToken cancellationToken)
    {
        var items = new List<SourcePlaylistItem>();
        var url = $"v1/playlists/{playlistId}/tracks?limit=100";

        while (url is not null)
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            foreach (var entry in doc.GetProperty("items").EnumerateArray())
            {
                if (!entry.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object)
                    continue;

                var id = track.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    ? idProp.GetString()! : "";
                var name = track.TryGetProperty("name", out var nameProp) ? nameProp.GetString()! : "";

                if (string.IsNullOrEmpty(id)) continue;

                items.Add(new SourcePlaylistItem
                {
                    Id = id,
                    Title = name,
                    ProviderIds = ParseTrackProviderIds(track)
                });
            }

            url = doc.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return items;
    }

    private static SourceMediaItem ParseTrack(JsonElement track, bool liked)
    {
        var id = track.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
            ? idProp.GetString()! : "";
        var name = track.TryGetProperty("name", out var nameProp) ? nameProp.GetString()! : "";

        var artistName = track.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array
            ? artists.EnumerateArray().FirstOrDefault().TryGetProperty("name", out var an) ? an.GetString() : null
            : null;
        var albumName = track.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object
            ? album.TryGetProperty("name", out var abn) ? abn.GetString() : null
            : null;

        return new SourceMediaItem
        {
            Id = id,
            Title = name,
            ProviderIds = ParseTrackProviderIds(track),
            PlayCount = 0,
            IsCompleted = false,
            Rating = liked ? 10.0 : null,
            MediaType = "music",
            ArtistName = artistName,
            AlbumName = albumName
        };
    }

    private static Dictionary<string, string> ParseTrackProviderIds(JsonElement track)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (track.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            providerIds["spotify"] = id.GetString()!;

        if (track.TryGetProperty("external_ids", out var extIds) && extIds.ValueKind == JsonValueKind.Object)
        {
            if (extIds.TryGetProperty("isrc", out var isrc) && isrc.ValueKind == JsonValueKind.String)
                providerIds["isrc"] = isrc.GetString()!;
        }

        return providerIds;
    }
}
