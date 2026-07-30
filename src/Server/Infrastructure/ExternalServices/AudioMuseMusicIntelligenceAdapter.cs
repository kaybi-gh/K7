using System.Net.Http.Json;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using K7.Server.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.ExternalServices;

public class AudioMuseMusicIntelligenceAdapter(
    HttpClient httpClient,
    IServerSettingsService serverSettingsService,
    ILogger<AudioMuseMusicIntelligenceAdapter> logger)
{
    private static readonly Dictionary<string, (string Mood, int CentroidIndex)> LegacyMoodAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chill"] = ("relaxed", 0),
        ["energetic"] = ("danceable", 0),
        ["happy"] = ("happy", 0),
        ["dark"] = ("sad", 0),
        ["focus"] = ("relaxed", 1),
    };

    public async Task<MusicIntelligenceConnectionResult> TestConnectionAsync(
        MusicIntelligenceSettingsDto? draftSettings = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (draftSettings is not null)
                ApplyClientSettings(draftSettings, requireEnabled: false);
            else
                await ConfigureClientAsync(cancellationToken, requireEnabled: false);

            var response = await httpClient.GetAsync("api/health", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new MusicIntelligenceConnectionResult(false, Error: $"HTTP {(int)response.StatusCode}");

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var version = content.TryGetProperty("version", out var v) ? v.GetString() : null;

            return new MusicIntelligenceConnectionResult(true, version);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Music intelligence connection test failed");
            return new MusicIntelligenceConnectionResult(false, Error: ex.Message);
        }
    }

    public async Task<List<MusicIntelligenceTrackMatchDto>> GetSimilarTracksAsync(
        Guid trackId,
        int count,
        string? title,
        string? artist,
        CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);
        var response = await httpClient.GetAsync(
            $"api/similar_tracks?item_id={trackId}&n={count}&eliminate_duplicates=true",
            cancellationToken);

        if (!response.IsSuccessStatusCode
            && !string.IsNullOrWhiteSpace(title)
            && !string.IsNullOrWhiteSpace(artist))
        {
            response = await httpClient.GetAsync(
                $"api/similar_tracks?title={Uri.EscapeDataString(title)}&artist={Uri.EscapeDataString(artist)}&n={count}&eliminate_duplicates=true",
                cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Similar tracks request failed with {StatusCode} for {TrackId}",
                (int)response.StatusCode, trackId);
            return [];
        }

        return ParseTrackMatches(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
    }

    public async Task<IReadOnlyList<MusicMoodPresetDto>> GetMoodPresetsAsync(CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);
        var response = await httpClient.GetAsync("api/mood_centroids", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (content.ValueKind != JsonValueKind.Object)
            return [];

        // AudioMuse returns dozens of centroids per mood; keep the strongest one per mood key.
        var bestByMood = new Dictionary<string, (MusicMoodPresetDto Preset, double Score)>(StringComparer.OrdinalIgnoreCase);
        foreach (var moodEntry in content.EnumerateObject())
        {
            if (moodEntry.Value.ValueKind != JsonValueKind.Array)
                continue;

            var fallbackIndex = 0;
            foreach (var centroid in moodEntry.Value.EnumerateArray())
            {
                var centroidIndex = fallbackIndex;
                if (centroid.TryGetProperty("index", out var indexProp) && indexProp.TryGetInt32(out var parsedIndex))
                    centroidIndex = parsedIndex;

                var moodScore = 0d;
                if (centroid.TryGetProperty("mood_score", out var scoreProp) && scoreProp.ValueKind == JsonValueKind.Number)
                    moodScore = scoreProp.GetDouble();

                string? topTags = null;
                if (centroid.TryGetProperty("top_tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    topTags = string.Join(", ", tags.EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(t => !string.IsNullOrWhiteSpace(t)));
                }

                var preset = new MusicMoodPresetDto
                {
                    MoodKey = moodEntry.Name,
                    CentroidIndex = centroidIndex,
                    TopTags = topTags
                };

                if (!bestByMood.TryGetValue(moodEntry.Name, out var existing) || moodScore > existing.Score)
                    bestByMood[moodEntry.Name] = (preset, moodScore);

                fallbackIndex++;
            }
        }

        return bestByMood.Values
            .Select(v => v.Preset)
            .OrderBy(p => p.MoodKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<Guid>> GetMoodTracksAsync(string moodKey, int centroidIndex, int count, CancellationToken cancellationToken)
    {
        var resolvedMoodKey = moodKey;
        var resolvedCentroidIndex = centroidIndex;
        if (LegacyMoodAliases.TryGetValue(moodKey, out var legacy))
        {
            resolvedMoodKey = legacy.Mood;
            resolvedCentroidIndex = legacy.CentroidIndex;
        }

        await ConfigureClientAsync(cancellationToken);
        var response = await httpClient.GetAsync(
            $"api/similar_tracks?mood={Uri.EscapeDataString(resolvedMoodKey)}&centroid_index={resolvedCentroidIndex}&n={count}&eliminate_duplicates=true",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Mood tracks request failed with {StatusCode} for {MoodKey}:{CentroidIndex}",
                (int)response.StatusCode, resolvedMoodKey, resolvedCentroidIndex);
            return [];
        }

        return ParseItemIds(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
    }

    public async Task<List<Guid>> GetDiscoveryTracksAsync(int count, CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);
        var response = await httpClient.GetAsync($"api/sonic_fingerprint/generate?n={count}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return ParseItemIds(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
    }

    public async Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            // Match AudioMuse UI default (PATH_DEFAULT_LENGTH / max_steps=25).
            var response = await httpClient.GetAsync(
                $"api/find_path?start_song_id={fromId}&end_song_id={toId}&max_steps=25",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Sonic path request failed with {StatusCode} for {FromId} -> {ToId} (attempt {Attempt})",
                    (int)response.StatusCode, fromId, toId, attempt + 1);

                if (attempt == 0
                    && response.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                        or System.Net.HttpStatusCode.GatewayTimeout
                        or System.Net.HttpStatusCode.RequestTimeout)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                return [];
            }

            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (!content.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.Array)
                return [];

            var ids = new List<Guid>();
            foreach (var item in path.EnumerateArray())
            {
                if (TryReadItemId(item, out var id))
                    ids.Add(id);
            }

            if (ids.Count > 0 || attempt == 1)
                return ids;

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        return [];
    }

    public async Task<List<Guid>> CreatePlaylistFromPromptAsync(string prompt, int count, CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);
        var request = new { userInput = $"{prompt}. Return up to {count} tracks." };
        var response = await httpClient.PostAsJsonAsync("api/chatPlaylist", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (content.TryGetProperty("response", out var responseNode)
            && responseNode.TryGetProperty("query_results", out var results))
            return ParseTrackMatches(results).Select(m => m.ItemId).ToList();

        if (content.TryGetProperty("query_results", out var flatResults))
            return ParseTrackMatches(flatResults).Select(m => m.ItemId).ToList();

        return [];
    }

    public async Task<IReadOnlyList<MusicSimilarArtistMatchDto>> GetSimilarArtistsAsync(
        Guid artistId,
        string? artistName,
        int count,
        CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);

        var response = await httpClient.GetAsync(
            $"api/similar_artists?artist_id={artistId}&n={count}",
            cancellationToken);

        if (!response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(artistName))
        {
            response = await httpClient.GetAsync(
                $"api/similar_artists?artist={Uri.EscapeDataString(artistName)}&n={count}",
                cancellationToken);
        }

        if (!response.IsSuccessStatusCode)
            return [];

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (content.ValueKind != JsonValueKind.Array)
            return [];

        var matches = new List<MusicSimilarArtistMatchDto>();
        foreach (var item in content.EnumerateArray())
        {
            string? id = null;
            if (item.TryGetProperty("artist_id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                id = idProp.GetString();

            string? name = null;
            if (item.TryGetProperty("artist", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                name = nameProp.GetString();

            double? divergence = null;
            if (item.TryGetProperty("divergence", out var divProp) && divProp.ValueKind == JsonValueKind.Number)
                divergence = divProp.GetDouble();

            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                continue;

            matches.Add(new MusicSimilarArtistMatchDto
            {
                ArtistId = id,
                Artist = name,
                Divergence = divergence
            });
        }

        return matches;
    }

    public async Task<List<Guid>> SearchTracksBySonicTextAsync(string query, int count, CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);
        await EnsureClapWarmAsync(cancellationToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var request = new { query, limit = count };
            var response = await httpClient.PostAsJsonAsync("api/clap/search", request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                logger.LogDebug("CLAP search unavailable (attempt {Attempt}), warming up again", attempt + 1);
                await EnsureClapWarmAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CLAP search failed with {StatusCode} for query {Query}", (int)response.StatusCode, query);
                return [];
            }

            var ids = ParseTrackMatches(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                .Select(m => m.ItemId)
                .ToList();

            if (ids.Count > 0 || attempt == 2)
                return ids;

            // Empty result on a cold index: warm again and retry before giving up.
            await EnsureClapWarmAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken);
        }

        return [];
    }

    public async Task<List<Guid>> SearchTracksByLyricsAsync(string query, int count, CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var request = new { query, limit = count };
            var response = await httpClient.PostAsJsonAsync("api/lyrics/search/text", request, cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                or System.Net.HttpStatusCode.GatewayTimeout
                or System.Net.HttpStatusCode.RequestTimeout)
            {
                await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Lyrics search failed with {StatusCode} for query {Query}", (int)response.StatusCode, query);
                return [];
            }

            var ids = ParseTrackMatches(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
                .Select(m => m.ItemId)
                .ToList();

            if (ids.Count > 0 || attempt == 2)
                return ids;

            await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken);
        }

        return [];
    }

    public async Task<bool> IsConfiguredAndEnabledAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await ReadSettingsAsync(cancellationToken);
            return settings is { Enabled: true } && !string.IsNullOrWhiteSpace(settings.BaseUrl);
        }
        catch
        {
            return false;
        }
    }

    public Task<MusicIntelligenceSettingsDto?> GetSettingsAsync(CancellationToken cancellationToken)
        => ReadSettingsAsync(cancellationToken);

    private async Task ConfigureClientAsync(CancellationToken cancellationToken, bool requireEnabled = true)
    {
        var settings = await ReadSettingsAsync(cancellationToken)
            ?? throw new InvalidOperationException("Music intelligence is not configured.");

        ApplyClientSettings(settings, requireEnabled);
    }

    private void ApplyClientSettings(MusicIntelligenceSettingsDto settings, bool requireEnabled)
    {
        if (requireEnabled && !settings.Enabled)
            throw new InvalidOperationException("Music intelligence is disabled.");

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new InvalidOperationException("Music intelligence base URL is not configured.");

        httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");

        httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
    }

    private async Task EnsureClapWarmAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsync("api/clap/warmup", content: null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("CLAP warmup returned {StatusCode}", (int)response.StatusCode);
                return;
            }

            // Give the freshly loaded ONNX model a brief moment before the first search.
            await Task.Delay(250, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CLAP warmup request failed");
        }
    }

    private async Task<MusicIntelligenceSettingsDto?> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.AudioMuseAi, cancellationToken);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<MusicIntelligenceSettingsDto>(json);
    }

    private static List<Guid> ParseItemIds(JsonElement? element)
    {
        return ParseTrackMatches(element).Select(m => m.ItemId).ToList();
    }

    private static List<MusicIntelligenceTrackMatchDto> ParseTrackMatches(JsonElement? element)
    {
        if (element is null)
            return [];

        if (element.Value.ValueKind == JsonValueKind.Object
            && element.Value.TryGetProperty("results", out var results))
            return ParseTrackMatchesFromArray(results);

        if (element.Value.ValueKind == JsonValueKind.Array)
            return ParseTrackMatchesFromArray(element.Value);

        return [];
    }

    private static List<MusicIntelligenceTrackMatchDto> ParseTrackMatchesFromArray(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return [];

        var matches = new List<MusicIntelligenceTrackMatchDto>();
        foreach (var item in array.EnumerateArray())
        {
            if (!TryReadItemId(item, out var id))
                continue;

            double? score = null;
            string? metric = null;
            if (item.ValueKind == JsonValueKind.Object)
            {
                if (TryReadScore(item, "distance", out var distance))
                {
                    score = distance;
                    metric = "distance";
                }
                else if (TryReadScore(item, "similarity", out var similarity))
                {
                    score = similarity;
                    metric = "similarity";
                }
                else if (TryReadScore(item, "score", out var rawScore))
                {
                    score = rawScore;
                    metric = "score";
                }
            }

            matches.Add(new MusicIntelligenceTrackMatchDto
            {
                ItemId = id,
                Score = score,
                ScoreMetric = metric
            });
        }

        return matches;
    }

    private static bool TryReadScore(JsonElement item, string propertyName, out double score)
    {
        score = default;
        if (!item.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Number)
            return false;

        score = prop.GetDouble();
        return true;
    }

    private static bool TryReadItemId(JsonElement item, out Guid id)
    {
        id = default;
        if (item.ValueKind == JsonValueKind.String)
            return Guid.TryParse(item.GetString(), out id);

        if (item.TryGetProperty("item_id", out var itemIdProp))
        {
            var raw = itemIdProp.ValueKind == JsonValueKind.String ? itemIdProp.GetString() : itemIdProp.ToString();
            return Guid.TryParse(raw, out id);
        }

        return false;
    }
}
