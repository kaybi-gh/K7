using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using K7.Import.Models;

namespace K7.Import.Sources.Tracearr;

/// <summary>
/// Tracearr public API v2 client (Tracearr 2.0+). API v1 is not supported.
/// </summary>
public sealed class TracearrClient : ISourceClient
{
    private const string ApiPrefix = "/api/v2/public";
    private const int PageSize = 100;

    private readonly HttpClient _httpClient;
    private readonly string? _serverFilter;
    private IReadOnlyList<TracearrServerInfo>? _servers;
    private IReadOnlyList<string>? _resolvedServerIds;

    public TracearrClient(string serverUrl, string apiKey, string? serverFilter = null)
    {
        _serverFilter = string.IsNullOrWhiteSpace(serverFilter) ? null : serverFilter.Trim();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/'))
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public IReadOnlyList<TracearrServerInfo> Servers => _servers ?? [];

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{ApiPrefix}/docs", cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                "Tracearr public API v2 was not found. K7.Import requires Tracearr 2.0 or later.");
        }

        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var version = doc.TryGetProperty("info", out var info) && info.TryGetProperty("version", out var ver)
            ? ver.GetString()
            : null;

        await EnsureServersResolvedAsync(cancellationToken);

        return new SourceServerInfo
        {
            Name = "Tracearr",
            Version = version
        };
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureServersResolvedAsync(cancellationToken);

        var users = new List<SourceUser>();
        string? cursor = null;

        while (true)
        {
            var path = BuildCursorPath($"{ApiPrefix}/users", cursor);
            var doc = await GetJsonAsync(path, cancellationToken);
            var data = doc.GetProperty("data");

            foreach (var user in data.EnumerateArray())
            {
                var id = user.GetProperty("id").GetString()!;
                var name = TryGetString(user, "username") ?? "Unknown";
                var detail = BuildUserDetail(user, _resolvedServerIds);

                // When filtering by server, skip identities with no remaining account on that server.
                if (_resolvedServerIds is { Count: > 0 } && detail is null
                    && !UserHasFilteredAccount(user, _resolvedServerIds))
                {
                    continue;
                }

                if (!users.Exists(u => u.Id == id))
                {
                    users.Add(new SourceUser
                    {
                        Id = id,
                        Name = name,
                        Detail = detail
                    });
                }
            }

            cursor = ReadNextCursor(doc);
            if (cursor is null)
                break;
        }

        return users;
    }

    public async Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureServersResolvedAsync(cancellationToken);

        if (_resolvedServerIds is { Count: > 0 })
        {
            var selected = Servers.Where(s => _resolvedServerIds.Contains(s.Id)).ToList();
            var label = selected.Count == 1
                ? $"{selected[0].Type} ({selected[0].Id})"
                : string.Join(", ", selected.Select(s => s.Type));
            return
            [
                new SourceLibrary
                {
                    Id = "filtered",
                    Name = $"Tracearr ({label})",
                    MediaType = null
                }
            ];
        }

        return
        [
            new SourceLibrary { Id = "all", Name = "All Servers", MediaType = null }
        ];
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(
        string libraryId,
        string userId,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureServersResolvedAsync(cancellationToken);

        if (_resolvedServerIds is null || _resolvedServerIds.Count == 0)
            return await FetchHistoryAsync(userId, serverId: null, progress, cancellationToken);

        // API accepts one server_id per request; merge when the filter matches several servers.
        if (_resolvedServerIds.Count == 1)
            return await FetchHistoryAsync(userId, _resolvedServerIds[0], progress, cancellationToken);

        var merged = new Dictionary<string, SourceMediaItem>(StringComparer.Ordinal);
        var serverIndex = 0;
        foreach (var serverId in _resolvedServerIds)
        {
            serverIndex++;
            var serverProgress = progress is null
                ? null
                : new Progress<string>(detail =>
                    progress.Report($"server {serverIndex}/{_resolvedServerIds.Count} - {detail}"));

            var items = await FetchHistoryAsync(userId, serverId, serverProgress, cancellationToken);
            foreach (var item in items)
            {
                if (merged.TryGetValue(item.Id, out var existing))
                {
                    merged[item.Id] = existing with
                    {
                        PlayCount = existing.PlayCount + item.PlayCount,
                        LastPlayedAt = item.LastPlayedAt > existing.LastPlayedAt ? item.LastPlayedAt : existing.LastPlayedAt,
                        IsCompleted = existing.IsCompleted || item.IsCompleted
                    };
                    existing.PlayHistory.AddRange(item.PlayHistory);
                }
                else
                {
                    merged[item.Id] = item;
                }
            }
        }

        return [.. merged.Values];
    }

    public Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<SourcePlaylist>());
    }

    private async Task EnsureServersResolvedAsync(CancellationToken cancellationToken)
    {
        if (_servers is not null)
            return;

        var doc = await GetJsonAsync($"{ApiPrefix}/libraries", cancellationToken);
        var servers = new Dictionary<string, TracearrServerInfo>(StringComparer.OrdinalIgnoreCase);

        if (doc.TryGetProperty("data", out var data) && data.ValueKind is JsonValueKind.Array)
        {
            foreach (var library in data.EnumerateArray())
            {
                var id = TryGetString(library, "server_id");
                if (id is null || servers.ContainsKey(id))
                    continue;

                var type = TryGetString(library, "server_type") ?? "unknown";
                servers[id] = new TracearrServerInfo(id, type);
            }
        }

        _servers = servers.Values.OrderBy(s => s.Type).ThenBy(s => s.Id).ToList();
        _resolvedServerIds = ResolveServerFilter(_serverFilter, _servers);
    }

    private static IReadOnlyList<string>? ResolveServerFilter(
        string? filter,
        IReadOnlyList<TracearrServerInfo> servers)
    {
        if (filter is null)
            return null;

        if (servers.Count == 0)
        {
            throw new InvalidOperationException(
                "Tracearr returned no media servers. Cannot apply --tracearr-server.");
        }

        var available = string.Join(", ", servers.Select(s => $"{s.Type}:{s.Id}"));

        // Exact server id
        var byId = servers.Where(s => string.Equals(s.Id, filter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byId.Count == 1)
            return [byId[0].Id];

        // UUID prefix
        var byPrefix = servers.Where(s => s.Id.StartsWith(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byPrefix.Count == 1)
            return [byPrefix[0].Id];
        if (byPrefix.Count > 1)
        {
            throw new InvalidOperationException(
                $"--tracearr-server '{filter}' matches multiple server ids. Use a full id. Available: {available}");
        }

        // server_type: plex / jellyfin / emby
        var byType = servers
            .Where(s => string.Equals(s.Type, filter, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Id)
            .ToList();
        if (byType.Count > 0)
            return byType;

        throw new InvalidOperationException(
            $"--tracearr-server '{filter}' matched no Tracearr server. Use plex|jellyfin|emby or a server id. Available: {available}");
    }

    private async Task<List<SourceMediaItem>> FetchHistoryAsync(
        string userId,
        string? serverId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var itemsByKey = new Dictionary<string, SourceMediaItem>();
        string? cursor = null;
        var sessionsProcessed = 0;
        var page = 1;

        progress?.Report("page 1...");

        while (true)
        {
            var extras = new List<(string Key, string Value)> { ("user_id", userId) };
            if (serverId is not null)
                extras.Add(("server_id", serverId));

            var path = BuildCursorPath($"{ApiPrefix}/history", cursor, [.. extras]);
            var doc = await GetJsonAsync(path, cancellationToken);
            var data = doc.GetProperty("data");

            foreach (var session in data.EnumerateArray())
            {
                sessionsProcessed++;

                var mediaTitle = TryGetString(session, "media_title");
                if (mediaTitle is null)
                    continue;

                var mediaType = TryGetString(session, "media_type");
                var watched = TryGetBool(session, "watched") == true;
                var startedAt = TryGetDateTime(session, "started_at");
                var durationMs = TryGetInt64(session, "duration_ms") ?? 0L;
                var durationSeconds = durationMs / 1000.0;

                // Music often arrives with empty artist_name; title is "Song - Artist".
                var artistName = TryGetString(session, "artist_name");
                var albumName = TryGetString(session, "album_name");
                if (mediaType is "track" && string.IsNullOrWhiteSpace(artistName))
                    TrySplitMusicTitle(mediaTitle, out mediaTitle, out artistName);

                var key = BuildMediaKey(session, mediaTitle, mediaType, artistName, albumName);

                var playEntry = startedAt is not null
                    ? new SourcePlayEntry
                    {
                        PlayedAt = startedAt.Value,
                        DurationSeconds = durationSeconds,
                        IsCompleted = watched,
                        IsTranscode = TryGetBool(session, "is_transcode"),
                        VideoDecision = TryGetString(session, "video_decision"),
                        AudioDecision = TryGetString(session, "audio_decision"),
                        Bitrate = TryGetInt32(session, "bitrate"),
                        SourceVideoCodec = TryGetString(session, "source_video_codec"),
                        SourceAudioCodec = TryGetString(session, "source_audio_codec"),
                        SourceVideoWidth = TryGetInt32(session, "source_video_width"),
                        SourceVideoHeight = TryGetInt32(session, "source_video_height"),
                        StreamVideoCodec = TryGetString(session, "stream_video_codec"),
                        StreamAudioCodec = TryGetString(session, "stream_audio_codec"),
                        DeviceName = TryGetString(session, "device"),
                        Platform = TryGetString(session, "platform"),
                        Player = TryGetString(session, "player")
                    }
                    : null;

                if (itemsByKey.TryGetValue(key, out var existing))
                {
                    itemsByKey[key] = existing with
                    {
                        PlayCount = existing.PlayCount + 1,
                        LastPlayedAt = startedAt > existing.LastPlayedAt ? startedAt : existing.LastPlayedAt,
                        IsCompleted = existing.IsCompleted || watched
                    };
                    if (playEntry is not null)
                        existing.PlayHistory.Add(playEntry);
                }
                else
                {
                    var item = new SourceMediaItem
                    {
                        Id = key,
                        Title = mediaTitle,
                        Year = TryGetInt32(session, "year"),
                        ProviderIds = ParseProviderIds(session),
                        PlayCount = 1,
                        LastPlayedAt = startedAt,
                        IsCompleted = watched,
                        Rating = null,
                        MediaType = mediaType switch
                        {
                            "movie" => "movie",
                            "episode" => "episode",
                            "track" => "music",
                            _ => mediaType
                        },
                        ArtistName = artistName,
                        AlbumName = albumName,
                        SeriesTitle = TryGetString(session, "show_title"),
                        SeasonNumber = TryGetInt32(session, "season_number"),
                        EpisodeNumber = TryGetInt32(session, "episode_number")
                    };

                    if (playEntry is not null)
                        item.PlayHistory.Add(playEntry);

                    itemsByKey[key] = item;
                }
            }

            cursor = ReadNextCursor(doc);
            progress?.Report(
                $"page {page} ({sessionsProcessed} sessions, {itemsByKey.Count} medias)" +
                (cursor is null ? "" : "..."));

            if (cursor is null)
                break;

            page++;
            progress?.Report($"page {page}...");
        }

        return [.. itemsByKey.Values];
    }

    private async Task<JsonElement> GetJsonAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                "Tracearr public API v2 was not found. K7.Import requires Tracearr 2.0 or later.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    private static string BuildCursorPath(string basePath, string? cursor, params (string Key, string Value)[] extra)
    {
        var sb = new StringBuilder(basePath);
        sb.Append("?pageSize=").Append(PageSize);
        if (cursor is not null)
            sb.Append("&cursor=").Append(Uri.EscapeDataString(cursor));

        foreach (var (key, value) in extra)
            sb.Append('&').Append(key).Append('=').Append(Uri.EscapeDataString(value));

        return sb.ToString();
    }

    private static string? ReadNextCursor(JsonElement doc)
    {
        if (!doc.TryGetProperty("meta", out var meta))
            return null;

        if (!meta.TryGetProperty("nextCursor", out var next) || next.ValueKind is JsonValueKind.Null)
            return null;

        var value = next.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool UserHasFilteredAccount(JsonElement user, IReadOnlyList<string> serverIds)
    {
        if (!user.TryGetProperty("accounts", out var accounts) || accounts.ValueKind is not JsonValueKind.Array)
            return false;

        foreach (var account in accounts.EnumerateArray())
        {
            if (TryGetString(account, "removed_at") is not null)
                continue;

            var serverId = TryGetString(account, "server_id");
            if (serverId is not null && serverIds.Contains(serverId, StringComparer.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? BuildUserDetail(JsonElement user, IReadOnlyList<string>? serverIdsFilter)
    {
        if (!user.TryGetProperty("accounts", out var accounts) || accounts.ValueKind is not JsonValueKind.Array)
            return null;

        var parts = new List<string>();
        foreach (var account in accounts.EnumerateArray())
        {
            if (TryGetString(account, "removed_at") is not null)
                continue;

            var serverId = TryGetString(account, "server_id");
            if (serverIdsFilter is { Count: > 0 }
                && (serverId is null || !serverIdsFilter.Contains(serverId, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            var serverType = TryGetString(account, "server_type");
            var accountName = TryGetString(account, "username");
            if (serverType is null && accountName is null)
                continue;

            parts.Add(accountName is null
                ? serverType!
                : serverType is null
                    ? accountName
                    : $"{accountName}@{serverType}");
        }

        return parts.Count == 0 ? null : string.Join(", ", parts.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void TrySplitMusicTitle(string mediaTitle, out string title, out string? artistName)
    {
        title = mediaTitle;
        artistName = null;
        var idx = LastTitleArtistSeparatorIndex(mediaTitle);
        if (idx <= 0 || idx + 3 >= mediaTitle.Length)
            return;

        title = mediaTitle[..idx].Trim();
        artistName = mediaTitle[(idx + 3)..].Trim();
        if (title.Length == 0 || artistName.Length == 0 || LastTitleArtistSeparatorIndex(artistName) >= 0)
        {
            title = mediaTitle;
            artistName = null;
        }
    }

    private static int LastTitleArtistSeparatorIndex(string title)
    {
        var hyphen = title.LastIndexOf(" - ", StringComparison.Ordinal);
        var enDash = title.LastIndexOf(" \u2013 ", StringComparison.Ordinal);
        var emDash = title.LastIndexOf(" \u2014 ", StringComparison.Ordinal);
        return Math.Max(hyphen, Math.Max(enDash, emDash));
    }

    private static string BuildMediaKey(
        JsonElement session,
        string mediaTitle,
        string? mediaType,
        string? artistName,
        string? albumName)
    {
        // Prefer Tracearr's canonical media identity when present (one row per title across servers).
        var mediaId = TryGetString(session, "media_id");
        if (!string.IsNullOrWhiteSpace(mediaId))
            return $"media:{mediaId}";

        return mediaType switch
        {
            "episode" => string.Join("|",
                "episode",
                TryGetString(session, "show_title") ?? "",
                TryGetInt32(session, "season_number")?.ToString() ?? "",
                TryGetInt32(session, "episode_number")?.ToString() ?? "",
                mediaTitle),
            "track" => string.Join("|",
                "track",
                artistName ?? "",
                albumName ?? "",
                mediaTitle),
            _ => string.Join("|",
                mediaType ?? "unknown",
                mediaTitle,
                TryGetInt32(session, "year")?.ToString() ?? "")
        };
    }

    private static Dictionary<string, string> ParseProviderIds(JsonElement session)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        TryAddId(providerIds, "tmdb", session, "tmdb_id");
        TryAddId(providerIds, "imdb", session, "imdb_id");
        TryAddId(providerIds, "tvdb", session, "tvdb_id");

        var ratingKey = TryGetString(session, "rating_key");
        if (!string.IsNullOrWhiteSpace(ratingKey))
        {
            var serverType = TryGetString(session, "server_type");
            var provider = serverType switch
            {
                "jellyfin" => "jellyfin",
                "emby" => "emby",
                _ => "plex"
            };
            providerIds.TryAdd(provider, ratingKey);
        }

        return providerIds;
    }

    private static void TryAddId(
        Dictionary<string, string> providerIds,
        string provider,
        JsonElement session,
        string propertyName)
    {
        if (!session.TryGetProperty(propertyName, out var value))
            return;

        var id = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(id))
            providerIds.TryAdd(provider, id);
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? TryGetBool(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is not JsonValueKind.Number)
            return null;

        return value.TryGetInt32(out var i) ? i : null;
    }

    private static long? TryGetInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind is not JsonValueKind.Number)
            return null;

        return value.TryGetInt64(out var i) ? i : null;
    }

    private static DateTime? TryGetDateTime(JsonElement element, string propertyName)
    {
        var raw = TryGetString(element, propertyName);
        if (raw is null)
            return null;

        return DateTime.Parse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }
}

public sealed record TracearrServerInfo(string Id, string Type);
