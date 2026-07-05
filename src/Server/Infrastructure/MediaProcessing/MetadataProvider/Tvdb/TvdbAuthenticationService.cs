using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using K7.Server.Application.Services;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

public sealed class TvdbAuthenticationService
{
    private const string LoginUrl = "https://api4.thetvdb.com/v4/login";
    private const int MaxAttempts = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly OutboundRateLimiter _rateLimiter;
    private readonly ILogger<TvdbAuthenticationService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public TvdbAuthenticationService(
        HttpClient httpClient,
        OutboundRateLimiter rateLimiter,
        ILogger<TvdbAuthenticationService> logger)
    {
        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null && _expiresAt > DateTimeOffset.UtcNow.AddHours(1))
            return _token;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _expiresAt > DateTimeOffset.UtcNow.AddHours(1))
                return _token;

            var body = new Dictionary<string, string> { ["apikey"] = TvdbDefaults.ApiKey };

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                await _rateLimiter.WaitAsync(TvdbApiClient.Host, cancellationToken);

                using var response = await _httpClient.PostAsJsonAsync(LoginUrl, body, JsonOptions, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? DefaultRetryAfter;
                    _rateLimiter.ReportRetryAfter(TvdbApiClient.Host, retryAfter);
                    _logger.LogDebug(
                        "TVDB login rate limited, retry after {RetryAfterSeconds}s",
                        retryAfter.TotalSeconds);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("TVDB login failed with status {StatusCode}", response.StatusCode);
                    return null;
                }

                var payload = await response.Content.ReadFromJsonAsync<TvdbApiResponse<TvdbLoginResponse>>(JsonOptions, cancellationToken);
                if (string.IsNullOrWhiteSpace(payload?.Data?.Token))
                {
                    _logger.LogWarning("TVDB login returned an empty token");
                    return null;
                }

                _token = payload.Data.Token;
                _expiresAt = DateTimeOffset.UtcNow.AddDays(29);
                return _token;
            }

            _logger.LogWarning("TVDB login exhausted retries after rate limiting");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "TVDB login failed");
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void InvalidateToken()
    {
        _token = null;
        _expiresAt = DateTimeOffset.MinValue;
    }
}
