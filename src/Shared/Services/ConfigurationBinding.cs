using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediaClient.Shared.Services;

public static class ConfigurationBinding
{
    public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaServerConfiguration>(configuration.GetSection("MediaServer"));
        return services;
    }
}
public class MediaServerConfiguration
{
    public string BaseUrl { get; set; } = "";
}
