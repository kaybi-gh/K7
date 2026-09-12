using System.Net;
using System.Reflection;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.ExternalServices.Federation;
using K7.Server.Infrastructure.ExternalServices.Scrobbling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace K7.Server.Infrastructure.ExternalServices;

public static class DependencyInjection
{
    public const string PeerStreamHttpClient = "PeerStream";

    public static IServiceCollection AddExternalServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<MusicIntelligenceHealthMonitor>();
        services.AddSingleton<PeerAccessTokenCache>();
        services.AddHttpClient<AudioMuseMusicIntelligenceAdapter>()
            .AddStandardResilienceHandler(options =>
            {
                // CLAP warmup + IVF pathfinding routinely exceed a few seconds on cold start.
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
                // SamplingDuration must be >= 2x AttemptTimeout for the standard handler.
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
                options.CircuitBreaker.MinimumThroughput = 8;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(20);
                options.Retry.MaxRetryAttempts = 2;
            });
        services.AddSingleton<IMusicIntelligenceCatalogReconciler, MusicIntelligenceCatalogReconciler>();
        services.AddScoped<IMusicIntelligenceService, MusicIntelligenceService>();

        // API / control-plane peer calls.
        services.AddHttpClient<IPeerClient, PeerClient>()
            .ConfigurePrimaryHttpMessageHandler(CreatePeerHttpHandler)
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(2);
                options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(IsPeerCircuitFailure(args.Outcome));
                // Keep retries for transient API faults, but align with circuit predicates.
                options.Retry.ShouldHandle = args => ValueTask.FromResult(IsPeerRetryFailure(args.Outcome));
            })
            .SelectPipelineByAuthority();

        // HLS / segment proxy: long attempt timeout (peer may wait ~90s for init),
        // circuit only on timeout / 500 / 403, no segment retries (player already retries).
        services.AddHttpClient(PeerStreamHttpClient)
            .ConfigurePrimaryHttpMessageHandler(CreatePeerHttpHandler)
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(120);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(150);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(240);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(IsPeerCircuitFailure(args.Outcome));
                options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
                options.Retry.MaxRetryAttempts = 1;
            })
            .SelectPipelineByAuthority();

        // Replace Aspire defaults with a short, capped pipeline so a flaky Yamtrack/BetaSeries
        // cannot sit on the queue for tens of seconds (playback stays on a separate path).
        var scrobbleVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        services.AddHttpClient(K7.Server.Application.DependencyInjection.ScrobbleHttpClient, client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd($"K7/{scrobbleVersion}");
                client.Timeout = TimeSpan.FromSeconds(20);
            })
            .ConfigureAdditionalHttpMessageHandlers((handlers, _) => handlers.Clear())
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 4;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(45);
                // 429 is handled per destination (Retry-After). Do not open the shared
                // scrobble circuit on rate limits (would block Last.fm/Trakt/webhooks too).
                options.CircuitBreaker.ShouldHandle = args =>
                    ValueTask.FromResult(IsScrobbleCircuitFailure(args.Outcome));
                // Resilience options require MaxRetryAttempts >= 1; disable retries via ShouldHandle.
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
                options.Retry.Delay = TimeSpan.FromMilliseconds(400);
            });

        services.AddScoped<IPeerApplicationManager, PeerApplicationManager>();

        services.AddSingleton<IScrobbleQueue, ScrobbleChannelQueue>();
        services.AddScoped<IScrobbleDestination, LastFmScrobbleDestination>();
        services.AddScoped<IScrobbleDestination, ListenBrainzScrobbleDestination>();
        services.AddScoped<IScrobbleDestination, TraktScrobbleDestination>();
        services.AddScoped<IScrobbleDestination, WebhookScrobbleDestination>();

        return services;
    }

    private static HttpClientHandler CreatePeerHttpHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false
    };

    /// <summary>
    /// Open the peer circuit only on hard failures: timeouts, 500, 403.
    /// 503 (e.g. segment still generating) and other 4xx must not isolate the peer.
    /// </summary>
    private static bool IsPeerCircuitFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is TimeoutRejectedException or HttpRequestException)
            return true;

        return outcome.Result?.StatusCode is HttpStatusCode.InternalServerError
            or HttpStatusCode.Forbidden
            or HttpStatusCode.RequestTimeout;
    }

    /// <summary>
    /// API retries: timeouts / connection errors / 500 / 408.
    /// Do not retry 403 or 429 (rate-limit retries amplify the problem).
    /// </summary>
    private static bool IsPeerRetryFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is TimeoutRejectedException or HttpRequestException)
            return true;

        return outcome.Result?.StatusCode is HttpStatusCode.InternalServerError
            or HttpStatusCode.RequestTimeout;
    }

    /// <summary>
    /// Shared scrobble HttpClient: trip only on hard failures, never on 429.
    /// </summary>
    private static bool IsScrobbleCircuitFailure(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is TimeoutRejectedException or HttpRequestException)
            return true;

        return outcome.Result?.StatusCode is HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.RequestTimeout;
    }
}
