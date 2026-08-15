using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using K7.Import.Matching;
using K7.Import.Models;

namespace K7.Import.Sources.Plex;

public sealed class PlexClient : ISourceClient
{
    // Plex metadata type IDs.
    private const int PlexTypeMovie = 1;
    private const int PlexTypeShow = 2;
    private const int PlexTypeEpisode = 4;
    private const int PlexTypeTrack = 10;
    private static readonly string ClientIdentifier = Guid.NewGuid().ToString();

    private readonly string _ownerToken;
    private readonly HttpClient _httpClient;
    private readonly HttpClient _plexTv;
    private readonly Dictionary<string, string> _tokensByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> _accessibleLibrariesByToken = new(StringComparer.Ordinal);
    private readonly List<string> _tokenWarnings = [];
    private Dictionary<string, string>? _libraryTypes;
    private string? _machineIdentifier;
    private string? _ownerName;
    private bool _tokensResolved;

    public bool IncludeDynamicPlaylists { get; init; }

    public IReadOnlyList<string> TokenWarnings => _tokenWarnings;

    public PlexClient(string serverUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
            throw new ArgumentException("--source-url is required for Plex (e.g. http://192.168.1.10:32400).");

        _ownerToken = NormalizeToken(token);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/'))
        };
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _plexTv = new HttpClient { BaseAddress = new Uri("https://plex.tv") };
        _plexTv.DefaultRequestHeaders.Add("Accept", "application/xml");
        _plexTv.DefaultRequestHeaders.Add("X-Plex-Token", _ownerToken);
        _plexTv.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", ClientIdentifier);
        _plexTv.DefaultRequestHeaders.Add("X-Plex-Product", "K7.Import");
        _plexTv.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var response = await PmsGetAsync("/", _ownerToken, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(
                "Plex returned 401 Unauthorized. Pass only the token value to --source-api-key " +
                "(the part after X-Plex-Token= in the View XML URL), not the header or query parameter name. " +
                "Also confirm --source-url points at your Plex Media Server (port 32400 by default).");
        }

        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var container = doc.GetProperty("MediaContainer");
        _machineIdentifier = container.TryGetProperty("machineIdentifier", out var mid)
            ? mid.GetString()
            : null;
        _ownerName = container.TryGetProperty("myPlexUsername", out var ownerName)
            ? ownerName.GetString()
            : null;
        IndexToken(_ownerToken, "owner", _ownerName);

        return new SourceServerInfo
        {
            Name = container.TryGetProperty("friendlyName", out var name) ? name.GetString() ?? "Plex" : "Plex",
            Version = container.TryGetProperty("version", out var ver) ? ver.GetString() : null
        };
    }

    private static string NormalizeToken(string token)
    {
        var value = token.Trim();
        const string prefix = "X-Plex-Token=";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            value = value[prefix.Length..].Trim();

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Plex token is empty. Copy the value after X-Plex-Token= from the View XML URL.");

        return value;
    }

    private Task<HttpResponseMessage> PmsGetAsync(string path, string token, CancellationToken cancellationToken)
    {
        var separator = path.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return _httpClient.GetAsync($"{path}{separator}X-Plex-Token={Uri.EscapeDataString(token)}", cancellationToken);
    }

    private string? TryGetUserToken(string userId) =>
        _tokensByKey.TryGetValue(userId, out var token) ? token : null;

    private void IndexToken(string token, params string?[] keys)
    {
        foreach (var key in keys)
        {
            if (!string.IsNullOrWhiteSpace(key))
                _tokensByKey.TryAdd(key, token);
        }
    }

    private void BindTokenAliases(string userId, string name)
    {
        if (_tokensByKey.ContainsKey(userId))
            return;

        if (_tokensByKey.TryGetValue(name, out var token))
            _tokensByKey.TryAdd(userId, token);
    }

    private async Task EnsureUserTokensAsync(CancellationToken cancellationToken)
    {
        if (_tokensResolved)
            return;

        _tokensResolved = true;
        IndexToken(_ownerToken, "owner", _ownerName);

        try
        {
            await LoadSharedServerTokensAsync(cancellationToken);
            await LoadHomeUserTokensAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _tokenWarnings.Add(
                "Could not reach plex.tv to resolve per-user tokens (" + ex.Message + "). " +
                "Only the Plex token owner will import ratings and playlists.");
        }
    }

    private async Task LoadSharedServerTokensAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_machineIdentifier))
            return;

        using var response = await _plexTv.GetAsync(
            $"/api/servers/{Uri.EscapeDataString(_machineIdentifier)}/shared_servers",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return;

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
            return;

        var doc = XDocument.Parse(xml);
        foreach (var server in doc.Descendants().Where(e => e.Name.LocalName is "SharedServer"))
        {
            var token = (string?)server.Attribute("accessToken");
            if (string.IsNullOrWhiteSpace(token))
                continue;

            IndexToken(
                token,
                (string?)server.Attribute("userID"),
                (string?)server.Attribute("username"),
                (string?)server.Attribute("email"),
                (string?)server.Attribute("title"));
        }
    }

    private async Task LoadHomeUserTokensAsync(CancellationToken cancellationToken)
    {
        using var response = await _plexTv.GetAsync("/api/home/users", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return;

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
            return;

        var doc = XDocument.Parse(xml);
        foreach (var user in doc.Descendants().Where(e => e.Name.LocalName is "User"))
        {
            var id = (string?)user.Attribute("id");
            var title = (string?)user.Attribute("title");
            var username = (string?)user.Attribute("username");
            var email = (string?)user.Attribute("email");
            var uuid = (string?)user.Attribute("uuid");
            var protectedPin = IsXmlTrue(user.Attribute("protected"));

            if (protectedPin)
            {
                if (!string.IsNullOrWhiteSpace(title))
                    _tokenWarnings.Add($"Plex Home user '{title}' has a PIN; ratings/playlists were skipped.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (TryGetUserToken(id) is not null
                || (!string.IsNullOrWhiteSpace(title) && TryGetUserToken(title) is not null))
            {
                BindTokenAliases(id, title ?? id);
                continue;
            }

            var token = await SwitchHomeUserAsync(id, uuid, cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                continue;

            IndexToken(token, id, uuid, title, username, email);
        }
    }

    private async Task<string?> SwitchHomeUserAsync(string id, string? uuid, CancellationToken cancellationToken)
    {
        foreach (var path in new[]
        {
            $"/api/home/users/{Uri.EscapeDataString(id)}/switch",
            string.IsNullOrWhiteSpace(uuid) ? null : $"/api/v2/home/users/{Uri.EscapeDataString(uuid)}/switch"
        })
        {
            if (path is null)
                continue;

            using var response = await _plexTv.PostAsync(path, content: null, cancellationToken);
            if (!response.IsSuccessStatusCode)
                continue;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
                continue;

            if (body.TrimStart().StartsWith('{'))
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("authToken", out var auth)
                    || json.RootElement.TryGetProperty("authenticationToken", out auth))
                {
                    return auth.GetString();
                }

                continue;
            }

            var xml = XDocument.Parse(body);
            var token = xml.Descendants().Select(e =>
                    (string?)e.Attribute("authenticationToken")
                    ?? (string?)e.Attribute("authToken"))
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (token is not null)
                return token;
        }

        return null;
    }

    private static bool IsXmlTrue(XAttribute? attribute)
    {
        var value = attribute?.Value;
        return value is "1" or "true" or "True";
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureUserTokensAsync(cancellationToken);

        var users = new List<SourceUser>();

        if (!string.IsNullOrWhiteSpace(_ownerName))
        {
            users.Add(new SourceUser
            {
                Id = "owner",
                Name = _ownerName
            });
        }

        try
        {
            var accountsResponse = await PmsGetAsync("/accounts", _ownerToken, cancellationToken);
            if (accountsResponse.IsSuccessStatusCode)
            {
                var accountsDoc = await accountsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                if (accountsDoc.TryGetProperty("MediaContainer", out var mc) &&
                    mc.TryGetProperty("Account", out var accounts))
                {
                    foreach (var account in accounts.EnumerateArray())
                    {
                        var id = account.GetProperty("id").ToString();
                        var name = account.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name) || id is "0")
                            continue;

                        if (users.All(u => !string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase)))
                            users.Add(new SourceUser { Id = id, Name = name });
                    }
                }
            }
        }
        catch
        {
            // /accounts may not be available on all setups
        }

        foreach (var user in users)
            BindTokenAliases(user.Id, user.Name);

        var skipped = users
            .Where(u => TryGetUserToken(u.Id) is null)
            .Select(u => u.Name)
            .ToList();
        if (skipped.Count > 0)
        {
            _tokenWarnings.Add(
                "Plex ratings/playlists for " + string.Join(", ", skipped) +
                " were skipped: no per-user token (PIN-protected Home user, or plex.tv share token unavailable). " +
                "History for those users still comes from Tautulli/Tracearr.");
        }

        return users;
    }

    public async Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var libraries = await LoadLibrariesAsync(cancellationToken);
        return libraries;
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var token = TryGetUserToken(userId);
        if (token is null)
            return [];

        var typesToFetch = await ResolvePlexTypesToFetchAsync(libraryId, cancellationToken);
        if (typesToFetch.Count == 0)
            return [];

        if (!await UserCanAccessLibraryAsync(token, libraryId, cancellationToken))
            return [];

        var items = new List<SourceMediaItem>();
        for (var typeIndex = 0; typeIndex < typesToFetch.Count; typeIndex++)
        {
            var plexType = typesToFetch[typeIndex];
            var typeLabel = PlexTypeLabel(plexType);
            var typePrefix = typesToFetch.Count > 1
                ? $"{typeLabel} ({typeIndex + 1}/{typesToFetch.Count})"
                : typeLabel;

            items.AddRange(await FetchLibraryItemsOfTypeAsync(
                libraryId, token, plexType, typePrefix, progress, cancellationToken));
        }

        return items;
    }

    private async Task<bool> UserCanAccessLibraryAsync(
        string token,
        string libraryId,
        CancellationToken cancellationToken)
    {
        if (!_accessibleLibrariesByToken.TryGetValue(token, out var libraryIds))
        {
            libraryIds = [];
            try
            {
                var response = await PmsGetAsync("/library/sections", token, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                    if (doc.TryGetProperty("MediaContainer", out var container)
                        && container.TryGetProperty("Directory", out var directories))
                    {
                        foreach (var dir in EnumerateJsonArrayOrObject(directories))
                        {
                            if (dir.TryGetProperty("key", out var key) && key.GetString() is { Length: > 0 } id)
                                libraryIds.Add(id);
                        }
                    }
                }
            }
            catch
            {
                // Treat as no libraries; per-request 403 handling still applies.
            }

            _accessibleLibrariesByToken[token] = libraryIds;
        }

        return libraryIds.Contains(libraryId);
    }

    private async Task<List<SourceMediaItem>> FetchLibraryItemsOfTypeAsync(
        string libraryId,
        string token,
        int plexType,
        string typePrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var items = new List<SourceMediaItem>();
        var offset = 0;
        const int pageSize = 100;
        var totalSize = 0;

        progress?.Report($"{typePrefix} page 1...");

        while (true)
        {
            var page = (offset / pageSize) + 1;
            if (totalSize > 0)
            {
                var totalPages = Math.Max(1, (int)Math.Ceiling(totalSize / (double)pageSize));
                progress?.Report($"{typePrefix} page {page}/{totalPages}...");
            }
            else if (page > 1)
            {
                progress?.Report($"{typePrefix} page {page}...");
            }

            var response = await PmsGetAsync(
                $"/library/sections/{libraryId}/all?type={plexType}&X-Plex-Container-Start={offset}&X-Plex-Container-Size={pageSize}&includeGuids=1",
                token,
                cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Forbidden
                or System.Net.HttpStatusCode.Unauthorized
                or System.Net.HttpStatusCode.NotFound)
            {
                return items;
            }

            response.EnsureSuccessStatusCode();
            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var container = doc.GetProperty("MediaContainer");

            if (!container.TryGetProperty("Metadata", out var metadata))
                break;

            var pageCount = 0;
            foreach (var item in metadata.EnumerateArray())
            {
                pageCount++;
                var parsed = ParseMediaItem(item);
                // Leaf playables + show containers (for series-level ratings).
                if (parsed.MediaType is "movie" or "episode" or "music" or "serie")
                    items.Add(parsed);
            }

            totalSize = container.TryGetProperty("totalSize", out var total) ? total.GetInt32() : offset + pageCount;
            var totalPagesDone = Math.Max(1, (int)Math.Ceiling(Math.Max(totalSize, 1) / (double)pageSize));
            progress?.Report(
                $"{typePrefix} page {page}/{totalPagesDone} ({Math.Min(offset + pageCount, totalSize)}/{totalSize} items, {items.Count} kept)");

            offset += pageSize;
            if (offset >= totalSize)
                break;
        }

        return items;
    }

    private static string PlexTypeLabel(int plexType) => plexType switch
    {
        PlexTypeMovie => "movies",
        PlexTypeShow => "shows",
        PlexTypeEpisode => "episodes",
        PlexTypeTrack => "tracks",
        _ => $"type-{plexType}"
    };

    private static IEnumerable<JsonElement> EnumerateJsonArrayOrObject(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                yield return item;
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
        }
    }

    private async Task<List<SourceLibrary>> LoadLibrariesAsync(CancellationToken cancellationToken)
    {
        var response = await PmsGetAsync("/library/sections", _ownerToken, cancellationToken);
        response.EnsureSuccessStatusCode();
        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var directories = doc.GetProperty("MediaContainer").GetProperty("Directory");

        var libraries = new List<SourceLibrary>();
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dir in directories.EnumerateArray())
        {
            var id = dir.GetProperty("key").GetString()!;
            var type = dir.GetProperty("type").GetString() ?? "unknown";
            libraries.Add(new SourceLibrary
            {
                Id = id,
                Name = dir.GetProperty("title").GetString()!,
                MediaType = type
            });
            types[id] = type;
        }

        _libraryTypes = types;
        return libraries;
    }

    private async Task<IReadOnlyList<int>> ResolvePlexTypesToFetchAsync(string libraryId, CancellationToken cancellationToken)
    {
        if (_libraryTypes is null)
            await LoadLibrariesAsync(cancellationToken);

        if (_libraryTypes is null || !_libraryTypes.TryGetValue(libraryId, out var libraryType))
            return [];

        return libraryType switch
        {
            "movie" => [PlexTypeMovie],
            // Episodes for history/ratings + shows for series-level userRating.
            "show" => [PlexTypeEpisode, PlexTypeShow],
            "artist" => [PlexTypeTrack],
            _ => []
        };
    }

    public async Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var playlists = new List<SourcePlaylist>();
        var token = TryGetUserToken(userId);
        if (token is null)
            return playlists;

        var response = await PmsGetAsync("/playlists", token, cancellationToken);
        if (!response.IsSuccessStatusCode) return playlists;

        var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!doc.GetProperty("MediaContainer").TryGetProperty("Metadata", out var playlistsArr))
            return playlists;

        foreach (var pl in playlistsArr.EnumerateArray())
        {
            var ratingKey = pl.GetProperty("ratingKey").GetString()!;
            var title = pl.GetProperty("title").GetString()!;
            var playlistType = pl.TryGetProperty("playlistType", out var pt) ? pt.GetString() : null;
            var isSmart = pl.TryGetProperty("smart", out var smartEl) &&
                (smartEl.ValueKind == JsonValueKind.True
                 || (smartEl.ValueKind == JsonValueKind.Number && smartEl.GetInt32() == 1)
                 || (smartEl.ValueKind == JsonValueKind.String && smartEl.GetString() is "1" or "true"));

            if (isSmart && !IncludeDynamicPlaylists)
            {
                playlists.Add(new SourcePlaylist
                {
                    Id = ratingKey,
                    Title = title,
                    IsDynamic = true,
                    MediaType = playlistType switch
                    {
                        "audio" => "music",
                        "video" => "video",
                        _ => null
                    }
                });
                continue;
            }

            var itemsResponse = await PmsGetAsync(
                $"/playlists/{ratingKey}/items?includeGuids=1", token, cancellationToken);
            if (!itemsResponse.IsSuccessStatusCode) continue;

            var itemsDoc = await itemsResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var playlistItems = new List<SourcePlaylistItem>();

            if (itemsDoc.GetProperty("MediaContainer").TryGetProperty("Metadata", out var itemsArr))
            {
                foreach (var item in itemsArr.EnumerateArray())
                {
                    var itemType = item.TryGetProperty("type", out var it) ? it.GetString() : null;
                    playlistItems.Add(new SourcePlaylistItem
                    {
                        Id = item.GetProperty("ratingKey").GetString()!,
                        Title = item.GetProperty("title").GetString()!,
                        ProviderIds = ParseGuids(item, itemType),
                        FilePaths = ParseFilePaths(item),
                        MediaType = itemType switch
                        {
                            "movie" => "movie",
                            "episode" => "episode",
                            "show" => "serie",
                            "track" => "music",
                            _ => null
                        },
                        ArtistName = itemType == "track"
                            ? (item.TryGetProperty("grandparentTitle", out var gpt) ? gpt.GetString() : null)
                            : null,
                        AlbumName = itemType == "track"
                            ? (item.TryGetProperty("parentTitle", out var albumTitle) ? albumTitle.GetString() : null)
                            : null,
                        Year = item.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number
                            ? y.GetInt32()
                            : null,
                        SeriesTitle = itemType == "episode"
                            ? (item.TryGetProperty("grandparentTitle", out var series) ? series.GetString() : null)
                            : null,
                        SeasonNumber = itemType == "episode" && item.TryGetProperty("parentIndex", out var season) && season.ValueKind == JsonValueKind.Number
                            ? season.GetInt32()
                            : null,
                        EpisodeNumber = itemType == "episode" && item.TryGetProperty("index", out var ep) && ep.ValueKind == JsonValueKind.Number
                            ? ep.GetInt32()
                            : null
                    });
                }
            }

            playlists.Add(new SourcePlaylist
            {
                Id = ratingKey,
                Title = title,
                IsDynamic = isSmart,
                MediaType = playlistType switch
                {
                    "audio" => "music",
                    "video" => "video",
                    _ => null
                },
                Items = playlistItems
            });
        }

        return playlists;
    }

    private static SourceMediaItem ParseMediaItem(JsonElement item)
    {
        var viewCount = item.TryGetProperty("viewCount", out var vc) ? vc.GetInt32() : 0;
        var durationMs = item.TryGetProperty("duration", out var dur) ? dur.GetInt64() : 0L;
        var durationSeconds = durationMs > 0 ? durationMs / 1000.0 : (double?)null;
        var viewOffsetMs = item.TryGetProperty("viewOffset", out var vo) ? vo.GetInt64() : 0L;
        var viewOffset = viewOffsetMs > 0 ? viewOffsetMs / 1000.0 : (double?)null;
        var lastViewedAt = item.TryGetProperty("lastViewedAt", out var lva)
            ? DateTimeOffset.FromUnixTimeSeconds(lva.GetInt64()).UtcDateTime
            : (DateTime?)null;
        var userRating = item.TryGetProperty("userRating", out var ur) ? ur.GetDouble() : (double?)null;
        var year = item.TryGetProperty("year", out var y) ? y.GetInt32() : (int?)null;
        var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;

        var isCompleted = false;
        if (durationMs > 0)
        {
            isCompleted = viewOffsetMs >= durationMs * 0.90
                || (viewCount > 0 && viewOffsetMs == 0 && lastViewedAt.HasValue);
        }

        return new SourceMediaItem
        {
            Id = item.GetProperty("ratingKey").GetString()!,
            Title = item.GetProperty("title").GetString()!,
            Year = year,
            ProviderIds = ParseGuids(item, type),
            FilePaths = ParseFilePaths(item),
            PlayCount = viewCount,
            LastPlaybackPosition = viewOffset,
            DurationSeconds = durationSeconds,
            LastPlayedAt = lastViewedAt,
            IsCompleted = isCompleted,
            Rating = userRating,
            MediaType = type switch
            {
                "movie" => "movie",
                "episode" => "episode",
                "show" => "serie",
                "track" => "music",
                _ => type
            },
            ArtistName = item.TryGetProperty("grandparentTitle", out var gpTitle) && type == "track" ? gpTitle.GetString() : null,
            AlbumName = item.TryGetProperty("parentTitle", out var pTitle) && type == "track" ? pTitle.GetString() : null,
            SeriesTitle = item.TryGetProperty("grandparentTitle", out var seriesTitle) && type == "episode" ? seriesTitle.GetString()
                : type == "show" ? item.TryGetProperty("title", out var showTitle) ? showTitle.GetString() : null
                : null,
            SeasonNumber = item.TryGetProperty("parentIndex", out var parentIdx) && type == "episode" ? parentIdx.GetInt32() : null,
            EpisodeNumber = item.TryGetProperty("index", out var idx) && type == "episode" ? idx.GetInt32() : null
        };
    }

    private static List<string> ParseFilePaths(JsonElement item)
    {
        var paths = new List<string>();
        if (!item.TryGetProperty("Media", out var mediaArr) || mediaArr.ValueKind != JsonValueKind.Array)
            return paths;

        foreach (var media in mediaArr.EnumerateArray())
        {
            if (!media.TryGetProperty("Part", out var parts) || parts.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var part in parts.EnumerateArray())
            {
                if (!part.TryGetProperty("file", out var fileEl))
                    continue;

                var file = fileEl.GetString();
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                paths.Add(NormalizePath(file));
            }
        }

        return paths;
    }

    internal static string NormalizePath(string path) =>
        path.Replace('\\', '/').Trim();

    private static Dictionary<string, string> ParseGuids(JsonElement item, string? plexType)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (item.TryGetProperty("Guid", out var guids) && guids.ValueKind == JsonValueKind.Array)
        {
            foreach (var guid in guids.EnumerateArray())
            {
                var id = guid.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (id is null) continue;
                TryAddNormalizedGuid(providerIds, id, plexType);
            }
        }

        // Some agents only expose the primary guid attribute.
        if (item.TryGetProperty("guid", out var primaryGuid) && primaryGuid.ValueKind == JsonValueKind.String)
            TryAddNormalizedGuid(providerIds, primaryGuid.GetString()!, plexType);

        return providerIds;
    }

    private static void TryAddNormalizedGuid(Dictionary<string, string> providerIds, string raw, string? plexType) =>
        PlexGuidParser.TryAdd(providerIds, raw, plexType);
}
