using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using K7.Import.Models;

namespace K7.Import.Sources.Tautulli;

public sealed partial class TautulliClient : ISourceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public TautulliClient(string serverUrl, string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/'))
        };
        _apiKey = apiKey;
    }

    private string Endpoint(string cmd) => $"/api/v2?apikey={Uri.EscapeDataString(_apiKey)}&cmd={cmd}";

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var doc = await _httpClient.GetFromJsonAsync<JsonElement>(Endpoint("get_server_info"), cancellationToken);
        var data = doc.GetProperty("response").GetProperty("data");
        return new SourceServerInfo
        {
            Name = data.TryGetProperty("pms_name", out var name) ? name.GetString() ?? "Tautulli" : "Tautulli",
            Version = data.TryGetProperty("pms_version", out var ver) ? ver.GetString() : null
        };
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var doc = await _httpClient.GetFromJsonAsync<JsonElement>(Endpoint("get_users_table") + "&length=1000", cancellationToken);
        var data = doc.GetProperty("response").GetProperty("data").GetProperty("data");
        var users = new List<SourceUser>();

        foreach (var user in data.EnumerateArray())
        {
            users.Add(new SourceUser
            {
                Id = user.GetProperty("user_id").ToString(),
                Name = user.GetProperty("friendly_name").GetString() ?? user.GetProperty("username").GetString() ?? "Unknown"
            });
        }

        return users;
    }

    public async Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        var doc = await _httpClient.GetFromJsonAsync<JsonElement>(Endpoint("get_libraries_table") + "&length=1000", cancellationToken);
        var data = doc.GetProperty("response").GetProperty("data").GetProperty("data");
        var libraries = new List<SourceLibrary>();

        foreach (var lib in data.EnumerateArray())
        {
            libraries.Add(new SourceLibrary
            {
                Id = lib.GetProperty("section_id").ToString(),
                Name = lib.GetProperty("section_name").GetString()!,
                MediaType = lib.TryGetProperty("section_type", out var st) ? st.GetString() : null,
                ItemCount = lib.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null
            });
        }

        return libraries;
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, CancellationToken cancellationToken = default)
    {
        var itemsByKey = new Dictionary<string, SourceMediaItem>();
        var start = 0;
        const int pageSize = 100;

        while (true)
        {
            var url = Endpoint("get_history") + $"&user_id={userId}&section_id={libraryId}&length={pageSize}&start={start}";
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);
            var response = doc.GetProperty("response").GetProperty("data");
            var data = response.GetProperty("data");

            foreach (var entry in data.EnumerateArray())
            {
                var ratingKey = entry.TryGetProperty("rating_key", out var rk) ? rk.ToString() : null;
                if (ratingKey is null) continue;

                var guid = entry.TryGetProperty("guid", out var g) ? g.GetString() : null;
                var providerIds = ParsePlexGuids(guid);

                var lastPlayedAt = entry.TryGetProperty("stopped", out var stopped) && stopped.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds(stopped.GetInt64()).UtcDateTime
                    : (DateTime?)null;

                var watchedStatus = entry.TryGetProperty("watched_status", out var ws) && ws.ValueKind == JsonValueKind.Number
                    ? ws.GetInt32() : 0;

                var mediaType = entry.TryGetProperty("media_type", out var mt) ? mt.GetString() : null;

                if (itemsByKey.TryGetValue(ratingKey, out var existing))
                {
                    itemsByKey[ratingKey] = existing with
                    {
                        PlayCount = existing.PlayCount + 1,
                        LastPlayedAt = lastPlayedAt > existing.LastPlayedAt ? lastPlayedAt : existing.LastPlayedAt,
                        IsCompleted = existing.IsCompleted || watchedStatus == 1
                    };
                }
                else
                {
                    var grandparentTitle = entry.TryGetProperty("grandparent_title", out var gpt) ? gpt.GetString() : null;
                    var parentTitle = entry.TryGetProperty("parent_title", out var pt) ? pt.GetString() : null;
                    var parentMediaIndex = entry.TryGetProperty("parent_media_index", out var pmi) && pmi.ValueKind == JsonValueKind.Number
                        ? pmi.GetInt32() : (int?)null;
                    var mediaIndex = entry.TryGetProperty("media_index", out var mi) && mi.ValueKind == JsonValueKind.Number
                        ? mi.GetInt32() : (int?)null;

                    itemsByKey[ratingKey] = new SourceMediaItem
                    {
                        Id = ratingKey,
                        Title = entry.TryGetProperty("full_title", out var ft) ? ft.GetString()! : entry.GetProperty("title").GetString()!,
                        Year = entry.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : null,
                        ProviderIds = providerIds,
                        PlayCount = 1,
                        LastPlayedAt = lastPlayedAt,
                        IsCompleted = watchedStatus == 1,
                        Rating = null,
                        MediaType = mediaType switch
                        {
                            "movie" => "movie",
                            "episode" => "episode",
                            "track" => "music",
                            _ => mediaType
                        },
                        ArtistName = mediaType == "track" ? grandparentTitle : null,
                        AlbumName = mediaType == "track" ? parentTitle : null,
                        SeriesTitle = mediaType == "episode" ? grandparentTitle : null,
                        SeasonNumber = mediaType == "episode" ? parentMediaIndex : null,
                        EpisodeNumber = mediaType == "episode" ? mediaIndex : null
                    };
                }
            }

            var totalCount = response.TryGetProperty("recordsFiltered", out var rc) ? rc.GetInt32() : 0;
            start += pageSize;
            if (start >= totalCount)
                break;
        }

        return [.. itemsByKey.Values];
    }

    public Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<SourcePlaylist>());
    }

    private static Dictionary<string, string> ParsePlexGuids(string? guid)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (guid is not null)
        {
            var match = PlexGuidRegex().Match(guid);
            if (match.Success)
            {
                providerIds.TryAdd(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Value);
            }
        }

        return providerIds;
    }

    [GeneratedRegex(@"plex://\w+/([a-z]+)://(.+)")]
    private static partial Regex PlexGuidRegex();
}
