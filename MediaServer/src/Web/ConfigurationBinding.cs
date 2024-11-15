using MediaServer.Infrastructure.Configuration;

namespace MediaServer.Web;

public static class ConfigurationBinding
{
    public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PathsConfiguration>(configuration.GetSection("Paths"));
        services.Configure<DatabaseConfiguration>(configuration.GetSection("Database"));
        return services;
    }
}
