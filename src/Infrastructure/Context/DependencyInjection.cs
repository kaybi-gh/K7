using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using MediaServer.Infrastructure.Context.Data;
using MediaServer.Infrastructure.Context.Data.Interceptors;
using MediaServer.Infrastructure.Context.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MediaServer.Infrastructure.Context;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseProvider = configuration.GetValue<string>("DatabaseProvider");
        var connectionString = databaseProvider switch
        {
            nameof(DatabaseProvider.Postgres) => configuration.GetConnectionString(nameof(DatabaseProvider.Postgres)),
            nameof(DatabaseProvider.Sqlite) => configuration.GetConnectionString(nameof(DatabaseProvider.Sqlite)),
            _ => throw new Exception($"Unsupported database provider: {databaseProvider}")
        };
        Guard.Against.Null(connectionString, message: $"Database {databaseProvider} connection string is empty.");

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            switch (databaseProvider)
            {
                case nameof(DatabaseProvider.Postgres):
                    options.UseNpgsql(connectionString!, x => x.MigrationsAssembly(DatabaseProvider.Postgres.Assembly));
                    break;

                case nameof(DatabaseProvider.Sqlite):
                    options.UseSqlite(connectionString!, x => x.MigrationsAssembly(DatabaseProvider.Sqlite.Assembly));
                    break;

                default:
                    throw new Exception($"Unsupported database provider: {databaseProvider}");
            }
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitialiser>();

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
}
