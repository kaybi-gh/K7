using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using K7.Server.Application.Services;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

public sealed class TvdbApiClient
{
    private const string BaseUrl = "https://api4.thetvdb.com/v4/";
    internal const string Host = "api4.thetvdb.com";
    private const int MaxAttempts = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly TvdbAuthenticationService _authentication;
    private readonly OutboundRateLimiter _rateLimiter;
    private readonly ILogger<TvdbApiClient> _logger;

    public TvdbApiClient(
        HttpClient httpClient,
        TvdbAuthenticationService authentication,
        OutboundRateLimiter rateLimiter,
        ILogger<TvdbApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _authentication = authentication;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TvdbDefaults.ApiKey);

    public async Task<IReadOnlyList<TvdbSearchResult>> SearchSeriesAsync(
        string query,
        int? year,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"query={Uri.EscapeDataString(query)}",
            "type=series"
        };

        if (year.HasValue)
            queryParams.Add($"year={year.Value}");

        var path = $"search?{string.Join('&', queryParams)}";
        var results = await GetAsync<List<TvdbSearchResult>>(path, cancellationToken);
        return results ?? [];
    }

    public async Task<TvdbSeriesExtended?> GetSeriesExtendedAsync(int seriesId, CancellationToken cancellationToken = default) =>
        await GetAsync<TvdbSeriesExtended>($"series/{seriesId}/extended", cancellationToken);

    public async Task<TvdbSeasonExtended?> GetSeasonExtendedAsync(int seasonId, CancellationToken cancellationToken = default) =>
        await GetAsync<TvdbSeasonExtended>($"seasons/{seasonId}/extended", cancellationToken);

    public async Task<TvdbEpisodeExtended?> GetEpisodeExtendedAsync(int episodeId, CancellationToken cancellationToken = default) =>
        await GetAsync<TvdbEpisodeExtended>($"episodes/{episodeId}/extended", cancellationToken);

    public async Task<TvdbTranslation?> GetSeriesTranslationAsync(
        int seriesId,
        string language,
        CancellationToken cancellationToken = default) =>
        await GetAsync<TvdbTranslation>($"series/{seriesId}/translations/{language}", cancellationToken);

    public async Task<TvdbTranslation?> GetSeasonTranslationAsync(
        int seasonId,
        string language,
        CancellationToken cancellationToken = default) =>
        await GetAsync<TvdbTranslation>($"seasons/{seasonId}/translations/{language}", cancellationToken);

    public async Task<TvdbTranslation?> GetEpisodeTranslationAsync(
        int episodeId,
        string language,
        CancellationToken cancellationToken = default) =>
        await GetAsync<TvdbTranslation>($"episodes/{episodeId}/translations/{language}", cancellationToken);

    public async Task<IReadOnlyList<TvdbEpisodeBase>> GetAllSeriesEpisodesAsync(
        int seriesId,
        CancellationToken cancellationToken = default)
    {
        var episodes = new List<TvdbEpisodeBase>();
        var page = 0;

        while (true)
        {
            var batch = await GetAsync<TvdbSeriesEpisodesPage>(
                $"series/{seriesId}/episodes/default?page={page}",
                cancellationToken);

            var pageEpisodes = batch?.Episodes;
            if (pageEpisodes is null || pageEpisodes.Count == 0)
                break;

            episodes.AddRange(pageEpisodes);
            page++;
        }

        return episodes;
    }

    public async Task<IReadOnlyList<TvdbArtwork>> GetSeriesArtworksAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        // Returns a series extended record, not a bare artwork list.
        var series = await GetAsync<TvdbSeriesExtended>($"series/{seriesId}/artworks", cancellationToken);
        return series?.Artworks ?? [];
    }

    public async Task<int?> FindSeriesIdByRemoteIdAsync(string remoteId, CancellationToken cancellationToken = default)
    {
        var results = await GetAsync<List<TvdbSearchByRemoteIdResult>>(
            $"search/remoteid/{Uri.EscapeDataString(remoteId)}",
            cancellationToken);

        return results?.FirstOrDefault(r => r.Series is not null)?.Series?.Id;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        if (!IsConfigured)
            return default;

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var token = await _authentication.GetTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
                return default;

            try
            {
                using var response = await SendAuthorizedAsync(path, token, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt < MaxAttempts - 1)
                {
                    _authentication.InvalidateToken();
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    ReportRateLimit(path, response);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("TVDB request failed for {Path} with status {StatusCode}", path, response.StatusCode);
                    return default;
                }

                var payload = await response.Content.ReadFromJsonAsync<TvdbApiResponse<T>>(JsonOptions, cancellationToken);
                return payload?.Data;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "TVDB request failed for {Path}", path);
                return default;
            }
        }

        _logger.LogWarning("TVDB request exhausted retries for {Path}", path);
        return default;
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        string path,
        string token,
        CancellationToken cancellationToken)
    {
        await _rateLimiter.WaitAsync(Host, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private void ReportRateLimit(string path, HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
        _rateLimiter.ReportRetryAfter(Host, retryAfter);
        _logger.LogDebug(
            "TVDB rate limited for {Path}, retry after {RetryAfterSeconds}s",
            path,
            retryAfter.TotalSeconds);
    }
}
