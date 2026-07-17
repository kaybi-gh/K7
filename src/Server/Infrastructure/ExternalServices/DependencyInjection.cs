using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.ExternalServices.Federation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace K7.Server.Infrastructure.ExternalServices;

public static class DependencyInjection
{
    public static IServiceCollection AddExternalServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<MusicIntelligenceHealthMonitor>();
        services.AddHttpClient<AudioMuseMusicIntelligenceAdapter>()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.MinimumThroughput = 3;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
            });
        services.AddScoped<IMusicIntelligenceService, MusicIntelligenceService>();

        services.AddHttpClient<IPeerClient, PeerClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            })
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(2);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
            })
            .SelectPipelineByAuthority();

        services.AddScoped<IPeerApplicationManager, PeerApplicationManager>();

        return services;
    }
}
