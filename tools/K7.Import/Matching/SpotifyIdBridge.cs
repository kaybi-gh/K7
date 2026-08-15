using System.Net.Http.Json;
using System.Text.Json;
using K7.Import.Models;

namespace K7.Import.Matching;

/// <summary>
/// Resolves Spotify track ids to ISRC without the Spotify catalog API.
/// Uses Odesli/Songlink (concurrent). MusicBrainz is not used here: its live API is
/// 1 request/sec with no reverse Spotify-id bulk route, and ListenBrainz labs only
/// map MBID to Spotify, not the other way.
/// </summary>
public sealed class SpotifyIdBridge
{
    private const string UserAgent = "K7-Import/1.0 (https://github.com/kaybi-gh/K7)";
    private readonly string? _cachePath;
    private readonly HttpClient _httpClient = new();

    public SpotifyIdBridge(string? cachePath)
    {
        _cachePath = cachePath;
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<List<SourceMediaItem>> EnrichAsync(
        List<SourceMediaItem> items,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var needed = items
            .Select(i => i.ProviderIds.TryGetValue("spotify", out var id) ? id : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .Where(id => items.Any(i =>
                i.ProviderIds.TryGetValue("spotify", out var sid)
                && string.Equals(sid, id, StringComparison.Ordinal)
                && !i.ProviderIds.ContainsKey("isrc")
                && !i.ProviderIds.ContainsKey("musicbrainz")))
            .ToList();

        if (needed.Count == 0)
            return items;

        var cache = LoadCache();
        var missing = needed.Where(id => !cache.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            progress?.Report($"odesli 0/{missing.Count}...");
            await LookupOdesliAsync(missing, cache, progress, cancellationToken);
            SaveCache(cache);
        }

        return [.. items.Select(item =>
        {
            if (!item.ProviderIds.TryGetValue("spotify", out var sid) || !cache.TryGetValue(sid, out var hit))
                return item;

            var ids = new Dictionary<string, string>(item.ProviderIds, StringComparer.OrdinalIgnoreCase);
            if (!ids.ContainsKey("isrc") && !string.IsNullOrWhiteSpace(hit.Isrc))
                ids["isrc"] = hit.Isrc;
            if (!ids.ContainsKey("musicbrainz") && !string.IsNullOrWhiteSpace(hit.MusicBrainz))
                ids["musicbrainz"] = hit.MusicBrainz;

            return ids.Count == item.ProviderIds.Count ? item : item with { ProviderIds = ids };
        })];
    }

    private async Task LookupOdesliAsync(
        List<string> spotifyIds,
        Dictionary<string, BridgeHit> cache,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var done = 0;
        using var gate = new SemaphoreSlim(4);
        var tasks = spotifyIds.Select(async id =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var hit = await FetchOdesliAsync(id, cancellationToken);
                var completed = Interlocked.Increment(ref done);
                if (completed % 25 == 0 || completed == spotifyIds.Count)
                    progress?.Report($"odesli {completed}/{spotifyIds.Count}");
                return (id, hit);
            }
            finally
            {
                gate.Release();
            }
        });

        foreach (var (id, hit) in await Task.WhenAll(tasks))
        {
            if (hit is not null)
                cache[id] = Merge(cache.GetValueOrDefault(id), hit);
        }
    }

    private async Task<BridgeHit?> FetchOdesliAsync(string spotifyId, CancellationToken cancellationToken)
    {
        try
        {
            var url = "https://api.song.link/v1-alpha.1/links"
                + $"?url={Uri.EscapeDataString("https://open.spotify.com/track/" + spotifyId)}"
                + "&userCountry=FR";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            var doc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (doc.ValueKind != JsonValueKind.Object
                || !doc.TryGetProperty("entitiesByUniqueId", out var entities)
                || entities.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? isrc = null;
            foreach (var entity in entities.EnumerateObject())
            {
                if (entity.Value.ValueKind != JsonValueKind.Object)
                    continue;
                if (entity.Value.TryGetProperty("isrc", out var isrcProp)
                    && isrcProp.ValueKind == JsonValueKind.String)
                {
                    isrc = isrcProp.GetString();
                    if (!string.IsNullOrWhiteSpace(isrc))
                        break;
                }
            }

            return string.IsNullOrWhiteSpace(isrc) ? null : new BridgeHit { Isrc = isrc };
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Dictionary<string, BridgeHit> LoadCache()
    {
        if (_cachePath is null || !File.Exists(_cachePath))
            return new Dictionary<string, BridgeHit>(StringComparer.Ordinal);

        try
        {
            var json = File.ReadAllText(_cachePath);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, BridgeHit>>(json);
            return loaded is null
                ? new Dictionary<string, BridgeHit>(StringComparer.Ordinal)
                : new Dictionary<string, BridgeHit>(loaded, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, BridgeHit>(StringComparer.Ordinal);
        }
        catch (IOException)
        {
            return new Dictionary<string, BridgeHit>(StringComparer.Ordinal);
        }
    }

    private void SaveCache(Dictionary<string, BridgeHit> cache)
    {
        if (_cachePath is null)
            return;

        try
        {
            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
        }
    }

    private static BridgeHit Merge(BridgeHit? existing, BridgeHit incoming) => new()
    {
        Isrc = string.IsNullOrWhiteSpace(existing?.Isrc) ? incoming.Isrc : existing.Isrc,
        MusicBrainz = string.IsNullOrWhiteSpace(existing?.MusicBrainz) ? incoming.MusicBrainz : existing.MusicBrainz
    };

    private sealed class BridgeHit
    {
        public string? Isrc { get; set; }
        public string? MusicBrainz { get; set; }
    }
}
