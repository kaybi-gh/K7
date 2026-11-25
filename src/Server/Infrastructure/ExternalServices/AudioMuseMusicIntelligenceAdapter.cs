using System.Net.Http.Json;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
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
            // Draft probes must not touch the shared client. A typed HttpClient rejects
            // BaseAddress changes after the first send, and discovery runs these calls in parallel.
            var endpoint = draftSettings is not null
                ? CreateEndpoint(draftSettings, requireEnabled: false)
                : await ResolveEndpointAsync(cancellationToken, requireEnabled: false);

            var response = await SendAsync(HttpMethod.Get, "api/health", endpoint, content: null, cancellationToken);

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
        var endpoint = await ResolveEndpointAsync(cancellationToken);
        var response = await SendAsync(
            HttpMethod.Get,
            $"api/similar_tracks?item_id={trackId}&n={count}&eliminate_duplicates=true",
            endpoint,
            content: null,
            cancellationToken);

        if (!response.IsSuccessStatusCode
            && !string.IsNullOrWhiteSpace(title)
            && !string.IsNullOrWhiteSpace(artist))
        {
            response = await SendAsync(
                HttpMethod.Get,
                $"api/similar_tracks?title={Uri.EscapeDataString(title)}&artist={Uri.EscapeDataString(artist)}&n={count}&eliminate_duplicates=true",
                endpoint,
                content: null,
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

    public async Task<List<Guid>> GetMoodTracksAsync(string moodKey, int centroidIndex, int count, CancellationToken cancellationToken)
    {
        var resolvedMoodKey = moodKey;
        var resolvedCentroidIndex = centroidIndex;
        if (LegacyMoodAliases.TryGetValue(moodKey, out var legacy))
        {
            resolvedMoodKey = legacy.Mood;
            resolvedCentroidIndex = legacy.CentroidIndex;
        }

        var endpoint = await ResolveEndpointAsync(cancellationToken);
        var response = await SendAsync(
            HttpMethod.Get,
            $"api/similar_tracks?mood={Uri.EscapeDataString(resolvedMoodKey)}&centroid_index={resolvedCentroidIndex}&n={count}&eliminate_duplicates=true",
            endpoint,
            content: null,
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
        var endpoint = await ResolveEndpointAsync(cancellationToken);
        var response = await SendAsync(
            HttpMethod.Get,
            $"api/sonic_fingerprint/generate?n={count}",
            endpoint,
            content: null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Discovery tracks request failed with {StatusCode}",
                (int)response.StatusCode);
            return [];
        }

        return ParseItemIds(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
    }

    public async Task<List<Guid>> GetSonicPathAsync(Guid fromId, Guid toId, CancellationToken cancellationToken)
    {
        var endpoint = await ResolveEndpointAsync(cancellationToken);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            // Match AudioMuse UI default (PATH_DEFAULT_LENGTH / max_steps=25).
            var response = await SendAsync(
                HttpMethod.Get,
                $"api/find_path?start_song_id={fromId}&end_song_id={toId}&max_steps=25",
                endpoint,
                content: null,
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
        var endpoint = await ResolveEndpointAsync(cancellationToken);
        var request = new { userInput = $"{prompt}. Return up to {count} tracks." };
        var response = await SendAsync(
            HttpMethod.Post,
            "api/chatPlaylist",
            endpoint,
            JsonContent.Create(request),
            cancellationToken);
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
        var endpoint = await ResolveEndpointAsync(cancellationToken);

        var response = await SendAsync(
            HttpMethod.Get,
            $"api/similar_artists?artist_id={artistId}&n={count}",
            endpoint,
            content: null,
            cancellationToken);

        if (!response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(artistName))
        {
            response = await SendAsync(
                HttpMethod.Get,
                $"api/similar_artists?artist={Uri.EscapeDataString(artistName)}&n={count}",
                endpoint,
                content: null,
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
        var endpoint = await ResolveEndpointAsync(cancellationToken);
        await EnsureClapWarmAsync(endpoint, cancellationToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await SendAsync(
                HttpMethod.Post,
                "api/clap/search",
                endpoint,
                JsonContent.Create(new { query, limit = count }),
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                logger.LogDebug("CLAP search unavailable (attempt {Attempt}), warming up again", attempt + 1);
                await EnsureClapWarmAsync(endpoint, cancellationToken);
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
            await EnsureClapWarmAsync(endpoint, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1 + attempt), cancellationToken);
        }

        return [];
    }

    public async Task<List<Guid>> SearchTracksByLyricsAsync(string query, int count, CancellationToken cancellationToken)
    {
        var endpoint = await ResolveEndpointAsync(cancellationToken);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await SendAsync(
                HttpMethod.Post,
                "api/lyrics/search/text",
                endpoint,
                JsonContent.Create(new { query, limit = count }),
                cancellationToken);

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

    public async Task StartServerAlignAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = await ResolveEndpointAsync(cancellationToken);

        using var response = await SendAsync(HttpMethod.Post, "api/servers/align", endpoint, content: null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("AudioMuse server align enqueued ({StatusCode})", (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "AudioMuse server align failed with {StatusCode}: {Body}",
            (int)response.StatusCode,
            body);
    }

    public async Task StartCleaningAsync(bool cleanCatalogue = true, CancellationToken cancellationToken = default)
    {
        var endpoint = await ResolveEndpointAsync(cancellationToken);

        using var response = await SendAsync(
            HttpMethod.Post,
            "api/cleaning/start",
            endpoint,
            JsonContent.Create(new { clean_catalogue = cleanCatalogue }),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "AudioMuse cleaning enqueued (cleanCatalogue={CleanCatalogue}, {StatusCode})",
                cleanCatalogue,
                (int)response.StatusCode);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "AudioMuse cleaning failed with {StatusCode}: {Body}",
            (int)response.StatusCode,
            body);
    }

    private readonly record struct AudioMuseEndpoint(Uri BaseUri, string? ApiKey);

    private async Task<AudioMuseEndpoint> ResolveEndpointAsync(CancellationToken cancellationToken, bool requireEnabled = true)
    {
        var settings = await ReadSettingsAsync(cancellationToken)
            ?? throw new InvalidOperationException("Music intelligence is not configured.");

        return CreateEndpoint(settings, requireEnabled);
    }

    private static AudioMuseEndpoint CreateEndpoint(MusicIntelligenceSettingsDto settings, bool requireEnabled)
    {
        if (requireEnabled && !settings.Enabled)
            throw new InvalidOperationException("Music intelligence is disabled.");

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
            throw new InvalidOperationException("Music intelligence base URL is not configured.");

        var baseUri = new Uri(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var apiKey = string.IsNullOrWhiteSpace(settings.ApiKey) ? null : settings.ApiKey;
        return new AudioMuseEndpoint(baseUri, apiKey);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        AudioMuseEndpoint endpoint,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(endpoint.BaseUri, relativePath))
        {
            Content = content
        };

        if (endpoint.ApiKey is not null)
            request.Headers.TryAddWithoutValidation("X-Api-Key", endpoint.ApiKey);

        // ResponseContentRead buffers the body before this returns, so disposing the
        // request afterwards does not drop the response the caller still has to read.
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task EnsureClapWarmAsync(AudioMuseEndpoint endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Post, "api/clap/warmup", endpoint, content: null, cancellationToken);
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
