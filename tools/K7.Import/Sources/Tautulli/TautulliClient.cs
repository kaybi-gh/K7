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
                var playEntry = ParsePlayEntry(entry, watchedStatus);

                if (itemsByKey.TryGetValue(ratingKey, out var existing))
                {
                    if (playEntry is not null)
                        existing.PlayHistory.Add(playEntry);

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

                    var item = new SourceMediaItem
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

                    if (playEntry is not null)
                        item.PlayHistory.Add(playEntry);

                    itemsByKey[ratingKey] = item;
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

    private static SourcePlayEntry? ParsePlayEntry(JsonElement entry, int watchedStatus)
    {
        var startedAt = ReadUnixTimestamp(entry, "date")
            ?? ReadDateTime(entry, "started");

        if (startedAt is null)
            return null;

        var stoppedAt = ReadUnixTimestamp(entry, "stopped");
        var watchedSeconds = ReadDouble(entry, "play_duration")
            ?? ReadDouble(entry, "duration");

        if (watchedSeconds is null or <= 0 && stoppedAt is not null)
            watchedSeconds = Math.Max(0, (stoppedAt.Value - startedAt.Value).TotalSeconds);

        var percentComplete = ReadInt(entry, "percent_complete") ?? 0;
        var isCompleted = watchedStatus == 1 || percentComplete >= 90;

        return new SourcePlayEntry
        {
            PlayedAt = startedAt.Value,
            DurationSeconds = watchedSeconds ?? 0,
            IsCompleted = isCompleted,
            IsTranscode = ReadString(entry, "transcode_decision") is { } transcode
                ? !string.Equals(transcode, "direct play", StringComparison.OrdinalIgnoreCase)
                : null,
            VideoDecision = ReadString(entry, "video_decision") ?? ReadString(entry, "stream_video_decision"),
            AudioDecision = ReadString(entry, "audio_decision") ?? ReadString(entry, "stream_audio_decision"),
            Bitrate = ReadInt(entry, "bitrate") ?? ReadInt(entry, "stream_bitrate"),
            SourceVideoCodec = ReadString(entry, "video_codec"),
            SourceAudioCodec = ReadString(entry, "audio_codec"),
            SourceVideoWidth = ReadInt(entry, "video_width"),
            SourceVideoHeight = ReadInt(entry, "video_height"),
            StreamVideoCodec = ReadString(entry, "stream_video_codec"),
            StreamAudioCodec = ReadString(entry, "stream_audio_codec"),
            DeviceName = ReadString(entry, "machine") ?? ReadString(entry, "machine_id"),
            Platform = ReadString(entry, "platform_name") ?? ReadString(entry, "platform"),
            Player = ReadString(entry, "player")
        };
    }

    private static DateTime? ReadUnixTimestamp(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value))
            return null;

        if (value.ValueKind == JsonValueKind.Number)
            return DateTimeOffset.FromUnixTimeSeconds(value.GetInt64()).UtcDateTime;

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;

        return null;
    }

    private static DateTime? ReadDateTime(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        return DateTime.TryParse(value.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? ReadString(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int? ReadInt(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : null,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static double? ReadDouble(JsonElement entry, string propertyName)
    {
        if (!entry.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
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
