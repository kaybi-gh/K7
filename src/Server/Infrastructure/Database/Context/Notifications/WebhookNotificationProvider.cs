using System.Text.Json;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.Database.Context.Notifications;

public class WebhookNotificationProvider : INotificationProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotificationProvider> _logger;

    public WebhookNotificationProvider(IHttpClientFactory httpClientFactory, ILogger<WebhookNotificationProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string configJson, string payload, CancellationToken cancellationToken = default)
    {
        var config = JsonSerializer.Deserialize<WebhookConfig>(configJson, JsonOptions);
        if (config is null || string.IsNullOrWhiteSpace(config.Url))
        {
            _logger.LogError("Webhook notification failed: invalid configuration");
            return false;
        }

        var client = _httpClientFactory.CreateClient();
        var method = new HttpMethod(config.Method ?? "POST");
        var request = new HttpRequestMessage(method, config.Url)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };

        if (config.Headers is { Count: > 0 })
        {
            foreach (var (key, value) in config.Headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }

        try
        {
            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Webhook sent successfully to {Url}", config.Url);
                return true;
            }

            _logger.LogWarning("Webhook to {Url} returned {StatusCode}", config.Url, response.StatusCode);
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Webhook to {Url} failed", config.Url);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Webhook to {Url} timed out", config.Url);
            throw;
        }
    }

    private sealed class WebhookConfig
    {
        public string? Url { get; set; }
        public string? Method { get; set; }
        public Dictionary<string, string>? Headers { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
