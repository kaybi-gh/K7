using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Infrastructure.ExternalServices;

public static class DependencyInjection
{
    public static IServiceCollection AddExternalServices(this IServiceCollection services)
    {
        services.AddHttpClient<IAudioMuseAiService, AudioMuseAiService>();
        return services;
    }
}
