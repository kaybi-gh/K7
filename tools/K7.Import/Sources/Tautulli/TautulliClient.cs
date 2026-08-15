using System.Net.Http.Json;
using System.Text.Json;
using K7.Import.Matching;
using K7.Import.Models;

namespace K7.Import.Sources.Tautulli;

public sealed class TautulliClient : ISourceClient
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

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var itemsByKey = new Dictionary<string, SourceMediaItem>();
        var seriesKeyByItem = new Dictionary<string, string>(StringComparer.Ordinal);
        var start = 0;
        const int pageSize = 100;
        var totalCount = 0;
        var totalPages = 0;
        var rowsProcessed = 0;

        progress?.Report("page 1...");

        while (true)
        {
            var page = (start / pageSize) + 1;
            if (totalPages > 0)
                progress?.Report($"page {page}/{totalPages}...");
            else if (page > 1)
                progress?.Report($"page {page}...");

            var url = Endpoint("get_history") + $"&user_id={userId}&section_id={libraryId}&length={pageSize}&start={start}";
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);
            var response = doc.GetProperty("response").GetProperty("data");
            var data = response.GetProperty("data");
            var pageRows = 0;

            foreach (var entry in data.EnumerateArray())
            {
                pageRows++;
                rowsProcessed++;
                var ratingKey = entry.TryGetProperty("rating_key", out var rk) ? rk.ToString() : null;
                if (ratingKey is null) continue;

                var lastPlayedAt = entry.TryGetProperty("stopped", out var stopped) && stopped.ValueKind == JsonValueKind.Number
                    ? DateTimeOffset.FromUnixTimeSeconds(stopped.GetInt64()).UtcDateTime
                    : (DateTime?)null;

                var watchedStatus = ReadDouble(entry, "watched_status") ?? 0;
                var isCompleted = watchedStatus >= 1.0;
                var percentComplete = ReadInt(entry, "percent_complete") ?? 0;

                var mediaType = entry.TryGetProperty("media_type", out var mt) ? mt.GetString() : null;
                var guid = entry.TryGetProperty("guid", out var g) ? g.GetString() : null;
                var providerIds = ParsePlexGuids(guid, mediaType);
                var playEntry = ParsePlayEntry(entry, isCompleted, percentComplete);

                if (itemsByKey.TryGetValue(ratingKey, out var existing))
                {
                    if (playEntry is not null)
                        existing.PlayHistory.Add(playEntry);

                    var nextProgress = Math.Max(existing.ProgressPercentage ?? 0, percentComplete);
                    itemsByKey[ratingKey] = existing with
                    {
                        PlayCount = existing.PlayCount + 1,
                        LastPlayedAt = lastPlayedAt > existing.LastPlayedAt ? lastPlayedAt : existing.LastPlayedAt,
                        IsCompleted = existing.IsCompleted || isCompleted,
                        ProgressPercentage = nextProgress > 0 ? nextProgress : existing.ProgressPercentage
                    };
                }
                else
                {
                    var grandparentTitle = ReadString(entry, "grandparent_title");
                    var parentTitle = ReadString(entry, "parent_title");
                    var parentMediaIndex = ReadInt(entry, "parent_media_index");
                    var mediaIndex = ReadInt(entry, "media_index");
                    var title = ReadString(entry, "title") ?? ReadString(entry, "full_title") ?? "";
                    var originalTitle = ReadString(entry, "original_title");
                    var grandparentRatingKey = entry.TryGetProperty("grandparent_rating_key", out var gprk)
                        ? gprk.ToString()
                        : null;
                    var isEpisode = mediaType == "episode" && !string.IsNullOrWhiteSpace(grandparentTitle);
                    var resolvedType = mediaType switch
                    {
                        "movie" => "movie",
                        "episode" when isEpisode => "episode",
                        "episode" => "movie",
                        "track" => "music",
                        _ => mediaType
                    };

                    var item = new SourceMediaItem
                    {
                        Id = ratingKey,
                        Title = title,
                        OriginalTitle = originalTitle,
                        Year = ReadInt(entry, "year"),
                        ProviderIds = providerIds,
                        PlayCount = 1,
                        LastPlayedAt = lastPlayedAt,
                        IsCompleted = isCompleted,
                        ProgressPercentage = percentComplete > 0 ? percentComplete : null,
                        Rating = null,
                        MediaType = resolvedType,
                        ArtistName = mediaType == "track" ? grandparentTitle : null,
                        AlbumName = mediaType == "track" ? parentTitle : null,
                        SeriesTitle = isEpisode ? grandparentTitle : null,
                        SeasonNumber = isEpisode ? parentMediaIndex : null,
                        EpisodeNumber = isEpisode ? mediaIndex : null
                    };

                    if (isEpisode && !string.IsNullOrWhiteSpace(grandparentRatingKey))
                        seriesKeyByItem[ratingKey] = grandparentRatingKey;

                    if (playEntry is not null)
                        item.PlayHistory.Add(playEntry);

                    itemsByKey[ratingKey] = item;
                }
            }

            totalCount = ReadInt(response, "recordsFiltered")
                ?? ReadInt(response, "recordsTotal")
                ?? totalCount;
            totalPages = totalCount > 0
                ? Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize))
                : Math.Max(page, totalPages);

            var rowsLabel = totalCount > 0
                ? $"{Math.Min(start + pageRows, totalCount)}/{totalCount}"
                : $"{rowsProcessed}";
            progress?.Report(
                $"page {page}/{Math.Max(totalPages, page)} ({rowsLabel} rows, {itemsByKey.Count} medias)");

            start += pageSize;
            if (totalCount > 0)
            {
                if (start >= totalCount)
                    break;
            }
            else if (pageRows < pageSize)
            {
                break;
            }
        }

        await EnrichSeriesGuidsAsync(itemsByKey, seriesKeyByItem, progress, cancellationToken);
        return [.. itemsByKey.Values];
    }

    public Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<SourcePlaylist>());
    }

    private static SourcePlayEntry? ParsePlayEntry(JsonElement entry, bool isCompleted, int percentComplete)
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

        var completed = isCompleted || percentComplete >= 90;

        return new SourcePlayEntry
        {
            PlayedAt = startedAt.Value,
            DurationSeconds = watchedSeconds ?? 0,
            IsCompleted = completed,
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
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.Number => (int)Math.Round(value.GetDouble()),
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsedDouble) => (int)Math.Round(parsedDouble),
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
            JsonValueKind.String when double.TryParse(value.GetString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private async Task EnrichSeriesGuidsAsync(
        Dictionary<string, SourceMediaItem> itemsByKey,
        Dictionary<string, string> seriesKeyByItem,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var seriesKeys = seriesKeyByItem.Values
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (seriesKeys.Count == 0)
            return;

        progress?.Report($"series guids 0/{seriesKeys.Count}...");
        var guidsBySeries = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var done = 0;
        using var gate = new SemaphoreSlim(6);

        var tasks = seriesKeys.Select(async seriesKey =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var guids = await FetchMetadataGuidsAsync(seriesKey, "show", cancellationToken);
                var completed = Interlocked.Increment(ref done);
                if (completed % 25 == 0 || completed == seriesKeys.Count)
                    progress?.Report($"series guids {completed}/{seriesKeys.Count}");

                return (seriesKey, guids);
            }
            finally
            {
                gate.Release();
            }
        });

        foreach (var (seriesKey, guids) in await Task.WhenAll(tasks))
        {
            if (guids.Count > 0)
                guidsBySeries[seriesKey] = guids;
        }

        if (guidsBySeries.Count == 0)
            return;

        foreach (var (itemId, seriesKey) in seriesKeyByItem)
        {
            if (!itemsByKey.TryGetValue(itemId, out var item))
                continue;
            if (!guidsBySeries.TryGetValue(seriesKey, out var guids))
                continue;

            itemsByKey[itemId] = item with
            {
                SeriesProviderIds = new Dictionary<string, string>(guids, StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    private async Task<Dictionary<string, string>> FetchMetadataGuidsAsync(
        string ratingKey,
        string plexType,
        CancellationToken cancellationToken)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var url = Endpoint("get_metadata") + $"&rating_key={Uri.EscapeDataString(ratingKey)}";
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(url, cancellationToken);
            if (doc.ValueKind != JsonValueKind.Object
                || !doc.TryGetProperty("response", out var response)
                || !response.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object)
            {
                return providerIds;
            }

            if (data.TryGetProperty("guids", out var guids) && guids.ValueKind == JsonValueKind.Array)
            {
                foreach (var guid in guids.EnumerateArray())
                    TryAddMetadataGuid(providerIds, guid, plexType);
            }

            if (data.TryGetProperty("guid", out var primary) && primary.ValueKind == JsonValueKind.String)
                PlexGuidParser.TryAdd(providerIds, primary.GetString(), plexType);
        }
        catch (HttpRequestException)
        {
            return providerIds;
        }
        catch (JsonException)
        {
            return providerIds;
        }

        return providerIds;
    }

    private static void TryAddMetadataGuid(Dictionary<string, string> providerIds, JsonElement guid, string plexType)
    {
        if (guid.ValueKind == JsonValueKind.String)
        {
            PlexGuidParser.TryAdd(providerIds, guid.GetString(), plexType);
            return;
        }

        if (guid.ValueKind == JsonValueKind.Object && guid.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String)
        {
            PlexGuidParser.TryAdd(providerIds, id.GetString(), plexType);
        }
    }

    private static Dictionary<string, string> ParsePlexGuids(string? guid, string? mediaType)
    {
        var providerIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        PlexGuidParser.TryAdd(providerIds, guid, mediaType);
        return providerIds;
    }
}
