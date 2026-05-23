using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Configuration;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Infrastructure.Database.Context.Data.Interceptors;
using K7.Server.Infrastructure.Database.Context.Identity;
using K7.Server.Infrastructure.Database.Context.Services;
using K7.Server.Infrastructure.Database.Context.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
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
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.ConfigureDbContext(sp.GetRequiredService<IOptions<DatabaseConfiguration>>().Value);
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

        var authConfiguration = configuration.GetSection("Authentication").Get<AuthenticationConfiguration>()!;
        var pathsConfiguration = configuration.GetSection("Paths").Get<PathsConfiguration>()!;
        var oidcKeysPath = Path.Combine(pathsConfiguration.Config, "openiddict-keys");

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

                options.AddEncryptionCertificate(LoadOrCreateCertificate(
                           Path.Combine(oidcKeysPath, "encryption-certificate.pfx"),
                           "CN=K7 OpenIddict Server Encryption Certificate"))
                       .AddSigningCertificate(LoadOrCreateCertificate(
                           Path.Combine(oidcKeysPath, "signing-certificate.pfx"),
                           "CN=K7 OpenIddict Server Signing Certificate"));

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
                options.AddEncryptionCertificate(LoadOrCreateCertificate(
                           Path.Combine(oidcKeysPath, "client-encryption-certificate.pfx"),
                           "CN=K7 OpenIddict Client Encryption Certificate"))
                       .AddSigningCertificate(LoadOrCreateCertificate(
                           Path.Combine(oidcKeysPath, "client-signing-certificate.pfx"),
                           "CN=K7 OpenIddict Client Signing Certificate"));

                options.UseSystemNetHttp();

                if (authConfiguration.Oidc.Enabled)
                {
                    options.AllowAuthorizationCodeFlow()
                           .AllowRefreshTokenFlow();

                    options.UseAspNetCore()
                           .EnableStatusCodePagesIntegration()
                           .EnableRedirectionEndpointPassthrough();

                    var oidcRegistration = new OpenIddictClientRegistration()
                    {
                        ProviderName = "oidc",
                        ProviderDisplayName = authConfiguration.Oidc.DisplayName,
                        Issuer = new Uri(authConfiguration.Oidc.Authority),
                        ClientId = authConfiguration.Oidc.ClientId,
                        ClientSecret = authConfiguration.Oidc.ClientSecret,
                        RedirectUri = new Uri("api/authentication/callback/login/oidc", UriKind.Relative),
                        PostLogoutRedirectUri = new Uri("api/authentication/callback/logout/oidc", UriKind.Relative),
                        ResponseTypes = { OpenIdConnectResponseType.Code },
                        Scopes = { Scopes.OpenId, Scopes.Email, Scopes.Profile }
                    };

                    foreach (var scope in authConfiguration.Oidc.Scopes.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
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


        services.AddSingleton(TimeProvider.System);
        services.AddTransient<IIdentityService, IdentityService>();
        services.AddScoped<IServerSettingsService, ServerSettingsService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();
        services.AddScoped<ISetupService, SetupService>();

        // Notification providers
        services.AddKeyedSingleton<INotificationProvider, WebhookNotificationProvider>(NotificationProviderType.Webhook);

        return services;
    }

    private static void ConfigureDbContext(this DbContextOptionsBuilder options, DatabaseConfiguration databaseConfiguration)
    {
        var connectionString = databaseConfiguration.BuildConnectionString();
        Guard.Against.Null(connectionString, message: $"Database {databaseConfiguration.Provider} connection string is empty.");

        switch (databaseConfiguration.Provider.ToLowerInvariant())
        {
            case "postgres":
                options.UseNpgsql(connectionString!, x => x
                    .MigrationsAssembly(DatabaseProvider.Postgres.Assembly))
                    .EnableSensitiveDataLogging();
                break;

            case "sqlite":
                options.UseSqlite(connectionString!, x => x
                    .MigrationsAssembly(DatabaseProvider.Sqlite.Assembly));
                break;

            default:
                throw new Exception($"Unsupported database provider: {databaseConfiguration.Provider}");
        }
    }

    private static string BuildConnectionString(this DatabaseConfiguration databaseConfiguration)
    {
        return databaseConfiguration.Provider.ToLowerInvariant() switch
        {
            "postgres" => $"User ID={databaseConfiguration.UserID};Password={databaseConfiguration.Password};Server={databaseConfiguration.Server};Port={databaseConfiguration.Port};Database={databaseConfiguration.Name};Include Error Detail=true;Maximum Pool Size=200;Timeout=30;",
            "sqlite" => $"Data Source={databaseConfiguration.Name}.db",
            _ => throw new Exception($"Unsupported database provider: {databaseConfiguration.Provider}")
        };
    }

    private static X509Certificate2 LoadOrCreateCertificate(string path, string subject)
    {
        if (File.Exists(path))
            return X509CertificateLoader.LoadPkcs12FromFile(path, null);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(2));
        var pfxBytes = certificate.Export(X509ContentType.Pfx);
        File.WriteAllBytes(path, pfxBytes);
        return X509CertificateLoader.LoadPkcs12(pfxBytes, null);
    }
}
