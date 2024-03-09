using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using MediaServer.Infrastructure.Configuration;
using MediaServer.Infrastructure.Context.Data;
using MediaServer.Infrastructure.Context.Data.Interceptors;
using MediaServer.Infrastructure.Context.Identity;
using MediaServer.Infrastructure.FileSystem;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MediaServer.Infrastructure.Context;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.ConfigureDbContext(services.BuildServiceProvider().GetRequiredService<IOptions<DatabaseConfiguration>>().Value);
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitializer>();
        services.AddScoped<ApplicationFileSystemInitializer>();

        services.AddAuthentication()
            .AddBearerToken(IdentityConstants.BearerScheme);

        services.AddAuthorizationBuilder();

        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddApiEndpoints();

        services.AddSingleton(TimeProvider.System);
        services.AddTransient<IIdentityService, IdentityService>();

        services.AddAuthorization(options =>
            options.AddPolicy(Policies.CanPurge, policy => policy.RequireRole(Roles.Administrator)));

        return services;
    }

    private static void ConfigureDbContext(this DbContextOptionsBuilder options, DatabaseConfiguration databaseConfiguration)
    {
        var connectionString = databaseConfiguration.BuildConnectionString();
        Guard.Against.Null(connectionString, message: $"Database {databaseConfiguration.Provider} connection string is empty.");

        switch (databaseConfiguration.Provider)
        {
            case nameof(DatabaseProvider.Postgres):
                options.UseNpgsql(connectionString!, x => x.MigrationsAssembly(DatabaseProvider.Postgres.Assembly));
                break;

            case nameof(DatabaseProvider.Sqlite):
                options.UseSqlite(connectionString!, x => x.MigrationsAssembly(DatabaseProvider.Sqlite.Assembly));
                break;

            default:
                throw new Exception($"Unsupported database provider: {databaseConfiguration.Provider}");
        }
    }

    private static string BuildConnectionString(this DatabaseConfiguration databaseConfiguration)
    {
        return databaseConfiguration.Provider.ToLowerInvariant() switch
        {
            "postgres" => $"User ID={databaseConfiguration.UserID};Password={databaseConfiguration.Password};Server={databaseConfiguration.Server};Port={databaseConfiguration.Port};Database={databaseConfiguration.Name};",
            "sqlite" => $"Data Source={databaseConfiguration.Name}.db",
            _ => throw new Exception($"Unsupported database provider: {databaseConfiguration.Provider}")
        };
    }
}
