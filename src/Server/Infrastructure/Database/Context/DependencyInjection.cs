using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Infrastructure.Database.Context.Data.Interceptors;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Server.Infrastructure.FileSystem;
using K7.Server.Infrastructure.Database.Context.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace K7.Server.Infrastructure.Database.Context;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, SlugInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.ConfigureDbContext(services.BuildServiceProvider().GetRequiredService<IOptions<DatabaseConfiguration>>().Value);
            options.UseOpenIddict();
        });

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = true; // TODO - In env variables?

            options.SignIn.RequireConfirmedEmail = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddSignInManager()
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

        var oidcConfiguration = services.BuildServiceProvider().GetRequiredService<IOptions<OidcConfiguration>>().Value;
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<ApplicationDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("connect/authorize")
                       .SetTokenEndpointUris("connect/token")
                       .SetUserInfoEndpointUris("connect/userinfo")
                       .SetEndSessionEndpointUris("connect/logout")
                       .SetDeviceAuthorizationEndpointUris("connect/device")
                       .SetEndUserVerificationEndpointUris("connect/verify");

                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow()
                       .AllowDeviceAuthorizationFlow()
                       .RequireProofKeyForCodeExchange();

                options.RegisterScopes("api", Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess);
                
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .EnableAuthorizationEndpointPassthrough()
                       .EnableEndSessionEndpointPassthrough()
                       .EnableTokenEndpointPassthrough()
                       .EnableUserInfoEndpointPassthrough()
                       .EnableEndUserVerificationEndpointPassthrough()
                       .EnableStatusCodePagesIntegration();

                options.AddEventHandler<ExtractTokenRequestContext>(builder =>
                {
                    builder.UseInlineHandler(static context =>
                    {
                        // The "scope" parameter is uncalled for for grant_type=authorization_code
                        // requests. To ensure OpenIddict won't reject token requests containing a
                        // "scope" parameter, this parameter is removed from the request instance.
                        if (context.Request is not null && context.Request.IsAuthorizationCodeGrantType())
                        {
                            context.Request.RemoveParameter(Parameters.Scope);
                        }

                        return ValueTask.CompletedTask;
                    });

                    builder.SetOrder(int.MaxValue);
                });
            })
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow()
                       .AllowRefreshTokenFlow();

                // Register the signing and encryption credentials used to protect
                // sensitive data like the state tokens produced by OpenIddict.
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
                options.UseAspNetCore()
                       .EnableStatusCodePagesIntegration()
                       .EnableRedirectionEndpointPassthrough();

                // Register the System.Net.Http integration and use the identity of the current
                // assembly as a more specific user agent, which can be useful when dealing with
                // providers that use the user agent as a way to throttle requests (e.g Reddit).
                options.UseSystemNetHttp();
                //.SetProductInformation(typeof(Program).Assembly);

                // Register the Web providers integrations.
                //
                // Note: to mitigate mix-up attacks, it's recommended to use a unique redirection endpoint
                // URI per provider, unless all the registered providers support returning a special "iss"
                // parameter containing their URL as part of authorization responses. For more information,
                // see https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics#section-4.4.

                if (oidcConfiguration.Enabled)
                {
                    var oidcRegistration = new OpenIddictClientRegistration()
                    {
                        ProviderName = "oidc",
                        ProviderDisplayName = oidcConfiguration.DisplayName,
                        Issuer = new Uri(oidcConfiguration.Authority),
                        ClientId = oidcConfiguration.ClientId,
                        ClientSecret = oidcConfiguration.ClientSecret,
                        RedirectUri = new Uri("api/authentication/callback/login/oidc", UriKind.Relative),
                        PostLogoutRedirectUri = new Uri("api/authentication/callback/logout/oidc", UriKind.Relative),
                        ResponseTypes = { OpenIdConnectResponseType.Code },
                        Scopes = { /*Scopes.OfflineAccess, */Scopes.OpenId, Scopes.Email, Scopes.Profile/*, "api1"*/ }
                    };

                    foreach (var scope in oidcConfiguration.Scopes.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        oidcRegistration.Scopes.Add(scope);
                    }

                    options.AddRegistration(oidcRegistration);
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextInitializer>();
        services.AddScoped<ApplicationFileSystemInitializer>();


        services.AddSingleton(TimeProvider.System);
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddScoped<IServerSettingsService, ServerSettingsService>();
        services.AddScoped<ISetupService, SetupService>();

        return services;
    }

    private static void ConfigureDbContext(this DbContextOptionsBuilder options, DatabaseConfiguration databaseConfiguration)
    {
        var connectionString = databaseConfiguration.BuildConnectionString();
        Guard.Against.Null(connectionString, message: $"Database {databaseConfiguration.Provider} connection string is empty.");

        switch (databaseConfiguration.Provider.ToLowerInvariant())
        {
            case "postgres":
                options.UseNpgsql(connectionString!, x => x.MigrationsAssembly(DatabaseProvider.Postgres.Assembly)).EnableSensitiveDataLogging();
                break;

            case "sqlite":
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
            "postgres" => $"User ID={databaseConfiguration.UserID};Password={databaseConfiguration.Password};Server={databaseConfiguration.Server};Port={databaseConfiguration.Port};Database={databaseConfiguration.Name};Include Error Detail=true;",
            "sqlite" => $"Data Source={databaseConfiguration.Name}.db",
            _ => throw new Exception($"Unsupported database provider: {databaseConfiguration.Provider}")
        };
    }
}
