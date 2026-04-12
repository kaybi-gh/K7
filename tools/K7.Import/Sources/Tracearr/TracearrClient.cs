using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using K7.Import.Models;

namespace K7.Import.Sources.Tracearr;

public sealed class TracearrClient : ISourceClient
{
    private readonly HttpClient _httpClient;

    public TracearrClient(string serverUrl, string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(serverUrl.TrimEnd('/'))
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var doc = await _httpClient.GetFromJsonAsync<JsonElement>("/api/v1/public/health", cancellationToken);
        return new SourceServerInfo
        {
            Name = "Tracearr",
            Version = doc.TryGetProperty("version", out var ver) ? ver.GetString() : null
        };
    }

    public async Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = new List<SourceUser>();
        var page = 1;
        const int pageSize = 100;

        while (true)
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(
                $"/api/v1/public/users?page={page}&pageSize={pageSize}", cancellationToken);

            var data = doc.GetProperty("data");
            foreach (var user in data.EnumerateArray())
            {
                var id = user.GetProperty("id").GetString()!;
                var name = user.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;
                name ??= user.TryGetProperty("username", out var un) ? un.GetString() : null;

                if (!users.Exists(u => u.Id == id))
                    users.Add(new SourceUser { Id = id, Name = name ?? "Unknown" });
            }

            var meta = doc.GetProperty("meta");
            var total = meta.GetProperty("total").GetInt32();
            if (page * pageSize >= total) break;
            page++;
        }

        return users;
    }

    public Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<SourceLibrary>
        {
            new() { Id = "all", Name = "All Servers", MediaType = null }
        });
    }

    public async Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, CancellationToken cancellationToken = default)
    {
        // Tracearr's /history endpoint returns sessions per user (embedded in the response).
        // We paginate through all history and aggregate per media item.
        var itemsByKey = new Dictionary<string, SourceMediaItem>();
        var page = 1;
        const int pageSize = 100;

        while (true)
        {
            var doc = await _httpClient.GetFromJsonAsync<JsonElement>(
                $"/api/v1/public/history?page={page}&pageSize={pageSize}", cancellationToken);

            var data = doc.GetProperty("data");
            foreach (var session in data.EnumerateArray())
            {
                // Filter to sessions for this specific user
                var user = session.GetProperty("user");
                var sessionUserId = user.GetProperty("id").GetString();
                if (sessionUserId != userId) continue;

                var mediaTitle = session.TryGetProperty("mediaTitle", out var mt) ? mt.GetString() : null;
                if (mediaTitle is null) continue;

                var mediaType = session.TryGetProperty("mediaType", out var mtp) ? mtp.GetString() : null;
                var watched = session.TryGetProperty("watched", out var w) && w.GetBoolean();

                var startedAt = session.TryGetProperty("startedAt", out var sa) && sa.ValueKind == JsonValueKind.String
                    ? DateTime.Parse(sa.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                    : (DateTime?)null;

                var durationMs = session.TryGetProperty("durationMs", out var dm) && dm.ValueKind == JsonValueKind.Number
                    ? dm.GetInt64() : 0L;
                var durationSeconds = durationMs / 1000.0;

                // Build a stable key from media identity
                var key = BuildMediaKey(session, mediaTitle, mediaType);

                var playEntry = startedAt is not null
                    ? new SourcePlayEntry
                    {
                        PlayedAt = startedAt.Value,
                        DurationSeconds = durationSeconds,
                        IsTranscode = session.TryGetProperty("isTranscode", out var isTr) && isTr.ValueKind == JsonValueKind.True ? true
                            : isTr.ValueKind == JsonValueKind.False ? false : null,
                        VideoDecision = session.TryGetProperty("videoDecision", out var vd) && vd.ValueKind == JsonValueKind.String ? vd.GetString() : null,
                        AudioDecision = session.TryGetProperty("audioDecision", out var ad) && ad.ValueKind == JsonValueKind.String ? ad.GetString() : null,
                        Bitrate = session.TryGetProperty("bitrate", out var br) && br.ValueKind == JsonValueKind.Number ? br.GetInt32() : null,
                        SourceVideoCodec = session.TryGetProperty("sourceVideoCodec", out var svc) && svc.ValueKind == JsonValueKind.String ? svc.GetString() : null,
                        SourceAudioCodec = session.TryGetProperty("sourceAudioCodec", out var sac) && sac.ValueKind == JsonValueKind.String ? sac.GetString() : null,
                        SourceVideoWidth = session.TryGetProperty("sourceVideoWidth", out var svw) && svw.ValueKind == JsonValueKind.Number ? svw.GetInt32() : null,
                        SourceVideoHeight = session.TryGetProperty("sourceVideoHeight", out var svh) && svh.ValueKind == JsonValueKind.Number ? svh.GetInt32() : null,
                        StreamVideoCodec = session.TryGetProperty("streamVideoCodec", out var stvc) && stvc.ValueKind == JsonValueKind.String ? stvc.GetString() : null,
                        StreamAudioCodec = session.TryGetProperty("streamAudioCodec", out var stac) && stac.ValueKind == JsonValueKind.String ? stac.GetString() : null,
                        DeviceName = session.TryGetProperty("device", out var dev) && dev.ValueKind == JsonValueKind.String ? dev.GetString() : null,
                        Platform = session.TryGetProperty("platform", out var plat) && plat.ValueKind == JsonValueKind.String ? plat.GetString() : null,
                        Player = session.TryGetProperty("player", out var pl) && pl.ValueKind == JsonValueKind.String ? pl.GetString() : null
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
                    var showTitle = session.TryGetProperty("showTitle", out var st) && st.ValueKind == JsonValueKind.String
                        ? st.GetString() : null;
                    var seasonNumber = session.TryGetProperty("seasonNumber", out var sn) && sn.ValueKind == JsonValueKind.Number
                        ? sn.GetInt32() : (int?)null;
                    var episodeNumber = session.TryGetProperty("episodeNumber", out var en) && en.ValueKind == JsonValueKind.Number
                        ? en.GetInt32() : (int?)null;
                    var year = session.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number
                        ? y.GetInt32() : (int?)null;
                    var artistName = session.TryGetProperty("artistName", out var an) && an.ValueKind == JsonValueKind.String
                        ? an.GetString() : null;
                    var albumName = session.TryGetProperty("albumName", out var aln) && aln.ValueKind == JsonValueKind.String
                        ? aln.GetString() : null;

                    var item = new SourceMediaItem
                    {
                        Id = key,
                        Title = mediaTitle,
                        Year = year,
                        ProviderIds = [],
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
                        SeriesTitle = showTitle,
                        SeasonNumber = seasonNumber,
                        EpisodeNumber = episodeNumber
                    };

                    if (playEntry is not null)
                        item.PlayHistory.Add(playEntry);

                    itemsByKey[key] = item;
                }
            }

            var meta = doc.GetProperty("meta");
            var total = meta.GetProperty("total").GetInt32();
            if (page * pageSize >= total) break;
            page++;
        }

        return [.. itemsByKey.Values];
    }

    public Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<SourcePlaylist>());
    }

    private static string BuildMediaKey(JsonElement session, string mediaTitle, string? mediaType)
    {
        // Build a stable composite key to aggregate sessions for the same media item
        return mediaType switch
        {
            "episode" => string.Join("|",
                "episode",
                session.TryGetProperty("showTitle", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : "",
                session.TryGetProperty("seasonNumber", out var sn) && sn.ValueKind == JsonValueKind.Number ? sn.GetInt32().ToString() : "",
                session.TryGetProperty("episodeNumber", out var en) && en.ValueKind == JsonValueKind.Number ? en.GetInt32().ToString() : "",
                mediaTitle),
            "track" => string.Join("|",
                "track",
                session.TryGetProperty("artistName", out var an) && an.ValueKind == JsonValueKind.String ? an.GetString() : "",
                session.TryGetProperty("albumName", out var aln) && aln.ValueKind == JsonValueKind.String ? aln.GetString() : "",
                mediaTitle),
            _ => string.Join("|",
                mediaType ?? "unknown",
                mediaTitle,
                session.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32().ToString() : "")
        };
    }
}
