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

    public async Task<MusicIntelligenceConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
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

    public async Task<List<Guid>> GetSimilarTracksAsync(Guid trackId, int count, CancellationToken cancellationToken)
    {
        await ConfigureClientAsync(cancellationToken);
        var response = await httpClient.GetAsync(
            $"api/similar_tracks?item_id={trackId}&n={count}&eliminate_duplicates=true",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return ParseItemIds(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
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

        var presets = new List<MusicMoodPresetDto>();
        foreach (var moodEntry in content.EnumerateObject())
        {
            if (moodEntry.Value.ValueKind != JsonValueKind.Array)
                continue;

            var centroidIndex = 0;
            foreach (var centroid in moodEntry.Value.EnumerateArray())
            {
                string? topTags = null;
                if (centroid.TryGetProperty("top_tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
                {
                    topTags = string.Join(", ", tags.EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(t => !string.IsNullOrWhiteSpace(t)));
                }

                presets.Add(new MusicMoodPresetDto
                {
                    MoodKey = moodEntry.Name,
                    CentroidIndex = centroidIndex,
                    TopTags = topTags
                });
                centroidIndex++;
            }
        }

        return presets;
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
        response.EnsureSuccessStatusCode();
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
        var response = await httpClient.GetAsync(
            $"api/find_path?start_song_id={fromId}&end_song_id={toId}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!content.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.Array)
            return [];

        var ids = new List<Guid>();
        foreach (var item in path.EnumerateArray())
        {
            if (TryReadItemId(item, out var id))
                ids.Add(id);
        }

        return ids;
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
            return ParseItemIds(results);

        if (content.TryGetProperty("query_results", out var flatResults))
            return ParseItemIds(flatResults);

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

    private async Task ConfigureClientAsync(CancellationToken cancellationToken, bool requireEnabled = true)
    {
        var settings = await ReadSettingsAsync(cancellationToken)
            ?? throw new InvalidOperationException("Music intelligence is not configured.");

        if (requireEnabled && !settings.Enabled)
            throw new InvalidOperationException("Music intelligence is disabled.");

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new InvalidOperationException("Music intelligence base URL is not configured.");

        httpClient.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");

        httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
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
        if (element is null || element.Value.ValueKind != JsonValueKind.Array)
            return [];

        var ids = new List<Guid>();
        foreach (var item in element.Value.EnumerateArray())
        {
            if (TryReadItemId(item, out var id))
                ids.Add(id);
        }

        return ids;
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
