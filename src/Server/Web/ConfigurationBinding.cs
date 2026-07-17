using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Configuration;

namespace K7.Server.Web;

public static class ConfigurationBinding
{
    public static IServiceCollection AddConfigurations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PathsConfiguration>(configuration.GetSection("Paths"));
        services.Configure<DatabaseConfiguration>(configuration.GetSection("Database"));
        services.Configure<AuthenticationConfiguration>(configuration.GetSection("Authentication"));
        services.AddOptions<SecurityConfiguration>()
            .Bind(configuration.GetSection("Security"))
            .Validate(
                security => !string.IsNullOrWhiteSpace(security.ApiKeys.HashSecret),
                "Security:ApiKeys:HashSecret is required. Set Security__ApiKeys__HashSecret (or Security__ApiKeys__HashSecret__File).")
            .ValidateOnStart();
        services.AddSingleton<IAuthenticationSettings, AuthenticationSettings>();
        return services;
    }
}
