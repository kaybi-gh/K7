using System.Net;
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
    private bool _hasUserProfile;
    private bool _catalogAvailable;
    private bool _useBatchTrackFetch = true;

    public List<string> TokenWarnings { get; } = [];

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
        if (!_hasApiToken)
            return new SourceServerInfo { Name = "Spotify", Version = null };

        using var me = await _httpClient.GetAsync("v1/me", cancellationToken);
        if (me.IsSuccessStatusCode)
        {
            _hasUserProfile = true;
            _catalogAvailable = true;
            return new SourceServerInfo { Name = "Spotify", Version = "user" };
        }

        // Client-credentials tokens cannot call /v1/me. Probe a single catalog track
        // (batch GET /v1/tracks?ids= and Search are restricted for new Dev Mode apps).
        using var probe = await _httpClient.GetAsync(
            "v1/tracks/11dFghVXANMlKmJXsNCbNl?market=FR", cancellationToken);
        if (probe.IsSuccessStatusCode)
        {
            _catalogAvailable = true;
            return new SourceServerInfo { Name = "Spotify", Version = "app" };
        }

        var detail = await ReadSpotifyErrorAsync(probe);
        var hint = "Spotify API token was rejected"
            + (string.IsNullOrWhiteSpace(detail) ? "." : $": {detail}")
            + " History matching will use titles only. New Dev Mode apps need the app owner on Spotify Premium; Search and batch GET /tracks are blocked.";

        if (_dataDir is not null && Directory.Exists(_dataDir))
        {
            TokenWarnings.Add(hint);
            return new SourceServerInfo { Name = "Spotify", Version = "export" };
        }

        throw new HttpRequestException($"Spotify API token was rejected ({(int)probe.StatusCode}): {detail}");
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        if (_hasUserProfile)
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

        foreach (var file in Directory.GetFiles(_dataDir, "*.json"))
        {
            JsonElement root;
            try
            {
                root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                continue;
            }

            if (root.ValueKind != JsonValueKind.Array) continue;

            foreach (var entry in root.EnumerateArray())
            {
                if (entry.TryGetProperty("username", out var username) && username.ValueKind == JsonValueKind.String)
                {
                    var name = username.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;
                }
            }
        }

        return null;
    }

    public bool HasStreamingHistoryExport()
    {
        if (_dataDir is null || !Directory.Exists(_dataDir)) return false;

        foreach (var file in Directory.GetFiles(_dataDir, "*.json"))
        {
            JsonElement root;
            try
            {
                root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                continue;
            }

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) continue;

            var sample = root[0];
            if (sample.TryGetProperty("ms_played", out _) || sample.TryGetProperty("msPlayed", out _))
                return true;
        }

        return false;
    }

    public bool HasPlaylistExport()
    {
        if (_dataDir is null || !Directory.Exists(_dataDir)) return false;
        return Directory.GetFiles(_dataDir, "Playlist*.json").Length > 0;
    }

    public Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = new List<SourceLibrary>();

        if (_hasUserProfile)
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

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        return libraryId switch
        {
            "saved-tracks" => await GetSavedTracksAsync(progress, cancellationToken),
            "saved-albums" => await GetSavedAlbumTracksAsync(progress, cancellationToken),
            "recently-played" => await GetRecentlyPlayedAsync(progress, cancellationToken),
            "streaming-history" => await HydrateSpotifyTrackMetadataAsync(
                LoadStreamingHistoryFromExport(progress), progress, cancellationToken),
            _ => []
        };
    }

    public async Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_hasUserProfile)
            return await GetPlaylistsFromApiAsync(cancellationToken);

        return LoadPlaylistsFromExport();
    }

    private async Task<List<SourcePlaylist>> GetPlaylistsFromApiAsync(CancellationToken cancellationToken)
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

    private List<SourcePlaylist> LoadPlaylistsFromExport()
    {
        if (_dataDir is null || !Directory.Exists(_dataDir))
            return [];

        var playlists = new List<SourcePlaylist>();
        var files = Directory.GetFiles(_dataDir, "Playlist*.json")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            JsonElement root;
            try
            {
                root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                continue;
            }

            // Account data export: { "playlists": [ ... ] } or legacy root array [ ... ]
            JsonElement playlistArray;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("playlists", out var nested)
                && nested.ValueKind == JsonValueKind.Array)
            {
                playlistArray = nested;
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                playlistArray = root;
            }
            else
            {
                continue;
            }

            var fileIndex = 0;
            foreach (var pl in playlistArray.EnumerateArray())
            {
                fileIndex++;
                var title = pl.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                    ? nameProp.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                if (!pl.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
                    continue;

                var playlistItems = new List<SourcePlaylistItem>();
                var trackIndex = 0;
                foreach (var entry in itemsEl.EnumerateArray())
                {
                    trackIndex++;
                    if (!entry.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object)
                        continue;

                    var trackUri = track.TryGetProperty("trackUri", out var uriProp) && uriProp.ValueKind == JsonValueKind.String
                        ? uriProp.GetString()
                        : null;
                    var trackName = track.TryGetProperty("trackName", out var tn) && tn.ValueKind == JsonValueKind.String
                        ? tn.GetString()
                        : null;
                    var artistName = track.TryGetProperty("artistName", out var an) && an.ValueKind == JsonValueKind.String
                        ? an.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(trackName) && string.IsNullOrWhiteSpace(trackUri))
                        continue;

                    string? spotifyId = null;
                    if (trackUri is not null && trackUri.StartsWith("spotify:track:", StringComparison.Ordinal))
                        spotifyId = trackUri["spotify:track:".Length..];

                    var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (spotifyId is not null)
                        providerIds["spotify"] = spotifyId;

                    playlistItems.Add(new SourcePlaylistItem
                    {
                        Id = spotifyId ?? $"{artistName}|{trackName}|{trackIndex}",
                        Title = trackName ?? spotifyId ?? $"Track {trackIndex}",
                        ProviderIds = providerIds,
                        ArtistName = artistName,
                        AlbumName = track.TryGetProperty("albumName", out var abn) && abn.ValueKind == JsonValueKind.String
                            ? abn.GetString()
                            : null
                    });
                }

                var fileStem = Path.GetFileNameWithoutExtension(file);
                playlists.Add(new SourcePlaylist
                {
                    Id = $"{fileStem}-{fileIndex}",
                    Title = title,
                    MediaType = "music",
                    Items = playlistItems
                });
            }
        }

        return playlists;
    }

    private async Task<List<SourceMediaItem>> GetSavedTracksAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var items = new List<SourceMediaItem>();
        var url = "v1/me/tracks?limit=50";
        var page = 0;

        while (url is not null)
        {
            page++;
            progress?.Report($"saved tracks page {page} ({items.Count} so far)...");
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);

            foreach (var entry in doc.GetProperty("items").EnumerateArray())
            {
                var track = entry.GetProperty("track");
                items.Add(ParseTrack(track, liked: true));
            }

            var total = doc.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == JsonValueKind.Number
                ? totalProp.GetInt32()
                : items.Count;
            progress?.Report($"saved tracks page {page} ({items.Count}/{total})");

            url = doc.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return items;
    }

    private async Task<List<SourceMediaItem>> GetSavedAlbumTracksAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var items = new List<SourceMediaItem>();
        var url = "v1/me/albums?limit=50";
        var page = 0;

        while (url is not null)
        {
            page++;
            progress?.Report($"saved albums page {page} ({items.Count} tracks so far)...");
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

            var total = doc.TryGetProperty("total", out var totalProp) && totalProp.ValueKind == JsonValueKind.Number
                ? totalProp.GetInt32()
                : page;
            progress?.Report($"saved albums page {page} ({items.Count} tracks, {total} albums)");

            url = doc.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return items;
    }

    private async Task<List<SourceMediaItem>> GetRecentlyPlayedAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var itemsByKey = new Dictionary<string, SourceMediaItem>();
        var url = "v1/me/player/recently-played?limit=50";

        progress?.Report("recently played...");
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
                    AlbumName = recentAlbumName,
                    Popularity = ParsePopularity(track)
                };
            }
        }

        progress?.Report($"recently played ({itemsByKey.Count} tracks)");
        return [.. itemsByKey.Values];
    }

    private List<SourceMediaItem> LoadStreamingHistoryFromExport(IProgress<string>? progress)
    {
        if (_dataDir is null || !Directory.Exists(_dataDir))
            return [];

        var itemsByUri = new Dictionary<string, SourceMediaItem>(StringComparer.OrdinalIgnoreCase);

        // Load all JSON files and filter by content (support all Spotify export naming conventions)
        var files = Directory.GetFiles(_dataDir, "*.json");
        progress?.Report($"streaming history (0/{files.Length} files)...");

        for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            var file = files[fileIndex];
            progress?.Report(
                $"streaming history ({fileIndex + 1}/{files.Length} files, {itemsByUri.Count} tracks) - {Path.GetFileName(file)}");

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

        progress?.Report($"streaming history done ({itemsByUri.Count} tracks)");
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

                var artistName = track.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array
                    ? artists.EnumerateArray().FirstOrDefault().TryGetProperty("name", out var an) ? an.GetString() : null
                    : null;
                var albumName = track.TryGetProperty("album", out var albumEl) && albumEl.ValueKind == JsonValueKind.Object
                    ? albumEl.TryGetProperty("name", out var abn) ? abn.GetString() : null
                    : null;

                items.Add(new SourcePlaylistItem
                {
                    Id = id,
                    Title = name,
                    ProviderIds = ParseTrackProviderIds(track),
                    ArtistName = artistName,
                    AlbumName = albumName,
                    Popularity = ParsePopularity(track)
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
            AlbumName = albumName,
            Popularity = ParsePopularity(track)
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

    private static int? ParsePopularity(JsonElement track)
    {
        if (track.TryGetProperty("popularity", out var popularity) && popularity.ValueKind == JsonValueKind.Number
            && popularity.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    private async Task<List<SourceMediaItem>> HydrateSpotifyTrackMetadataAsync(
        List<SourceMediaItem> items,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 || !_hasApiToken || !_catalogAvailable)
            return items;

        var spotifyIds = items
            .Select(i => i.ProviderIds.TryGetValue("spotify", out var id) ? id : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (spotifyIds.Count == 0)
            return items;

        var isrcById = new Dictionary<string, string>(StringComparer.Ordinal);
        var albumById = new Dictionary<string, string>(StringComparer.Ordinal);
        var popularityById = new Dictionary<string, int>(StringComparer.Ordinal);
        var done = 0;

        foreach (var chunk in spotifyIds.Chunk(50))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tracks = await GetTracksByIdsAsync(chunk, cancellationToken);
            if (tracks is null)
            {
                progress?.Report("spotify metadata skipped (API token rejected or unavailable)");
                break;
            }

            foreach (var track in tracks)
            {
                if (track.ValueKind != JsonValueKind.Object)
                    continue;

                var id = track.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    ? idProp.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var ids = ParseTrackProviderIds(track);
                if (ids.TryGetValue("isrc", out var isrc))
                    isrcById.TryAdd(id, isrc);

                if (track.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object
                    && album.TryGetProperty("name", out var albumName) && albumName.ValueKind == JsonValueKind.String)
                {
                    var name = albumName.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        albumById.TryAdd(id, name);
                }

                var popularity = ParsePopularity(track);
                if (popularity is not null)
                    popularityById.TryAdd(id, popularity.Value);
            }

            done += chunk.Length;
            progress?.Report($"spotify metadata {Math.Min(done, spotifyIds.Count)}/{spotifyIds.Count}");
        }

        if (isrcById.Count == 0 && albumById.Count == 0 && popularityById.Count == 0)
            return items;

        return [.. items.Select(item =>
        {
            if (!item.ProviderIds.TryGetValue("spotify", out var sid))
                return item;

            var ids = new Dictionary<string, string>(item.ProviderIds, StringComparer.OrdinalIgnoreCase);
            if (isrcById.TryGetValue(sid, out var isrc))
                ids["isrc"] = isrc;

            var albumName = item.AlbumName;
            if (string.IsNullOrWhiteSpace(albumName) && albumById.TryGetValue(sid, out var hydratedAlbum))
                albumName = hydratedAlbum;

            var popularity = item.Popularity;
            if (popularityById.TryGetValue(sid, out var hydratedPopularity))
                popularity = hydratedPopularity;

            return item with { ProviderIds = ids, AlbumName = albumName, Popularity = popularity };
        })];
    }

    private async Task<List<JsonElement>?> GetTracksByIdsAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        if (_useBatchTrackFetch)
        {
            var batched = await TryGetTracksBatchAsync(ids, cancellationToken);
            if (batched is not null)
                return batched;

            _useBatchTrackFetch = false;
        }

        var tracks = new List<JsonElement>(ids.Count);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var track = await GetTrackByIdAsync(id, cancellationToken);
            if (track is null)
                return tracks.Count > 0 ? tracks : null;

            tracks.Add(track.Value);
        }

        return tracks;
    }

    private async Task<List<JsonElement>?> TryGetTracksBatchAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var url = $"v1/tracks?ids={string.Join(",", ids)}&market=FR";
        using var response = await SendWithRetryAsync(url, cancellationToken);
        if (response is null)
            return null;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (doc.ValueKind != JsonValueKind.Object || !doc.TryGetProperty("tracks", out var tracks)
            || tracks.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. tracks.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.Object)];
    }

    private async Task<JsonElement?> GetTrackByIdAsync(string id, CancellationToken cancellationToken)
    {
        var url = $"v1/tracks/{Uri.EscapeDataString(id)}?market=FR";
        using var response = await SendWithRetryAsync(url, cancellationToken);
        if (response is null)
            return null;

        if (response.StatusCode is HttpStatusCode.NotFound)
            return null;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return null;

        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return doc.ValueKind == JsonValueKind.Object ? doc : null;
    }

    private async Task<HttpResponseMessage?> SendWithRetryAsync(string url, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if ((int)response.StatusCode == 429 || response.StatusCode is HttpStatusCode.ServiceUnavailable)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                if (response.Headers.RetryAfter?.Delta is TimeSpan retryAfter)
                    delay = retryAfter;
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            return response;
        }

        return null;
    }

    private static async Task<string> ReadSpotifyErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
            return $"{(int)response.StatusCode} {response.ReasonPhrase}";

        try
        {
            var json = JsonSerializer.Deserialize<JsonElement>(text);
            if (json.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? text;
            }
        }
        catch (JsonException)
        {
        }

        return text.Length > 300 ? text[..300] : text;
    }
}
