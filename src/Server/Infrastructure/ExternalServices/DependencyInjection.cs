using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Infrastructure.ExternalServices;

public static class DependencyInjection
{
    public static IServiceCollection AddExternalServices(this IServiceCollection services)
    {
        services.AddMemoryCache(options => options.SizeLimit = 10_000);
        services.AddHttpClient<AudioMuseMusicIntelligenceAdapter>();
        services.AddScoped<IMusicIntelligenceService, MusicIntelligenceService>();
        return services;
    }
}
