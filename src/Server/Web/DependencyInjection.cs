using K7.Shared.Json;
using K7.Server.Domain.Constants;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Infrastructure.Configuration;
using K7.Server.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using K7.Server.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Http.Resilience;
using OpenIddict.Validation.AspNetCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace K7.Server.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddMemoryCache();

        var configPath = configuration.GetValue<string>("Paths:Config") ?? "config";
        var keysPath = Path.Combine(configPath, "dataprotection-keys");
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("K7");

        var forceHttps = configuration.GetValue<bool?>("Security:ForceHttps") ?? true;
        var cookieSecurePolicy = forceHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = forceHttps ? "__Host-X-XSRF-TOKEN" : ".K7.Antiforgery";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = cookieSecurePolicy;
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            var knownProxies = configuration.GetSection("Security:KnownProxies").Get<string[]>();
            var trustPrivateProxies = configuration.GetValue<bool?>("Security:TrustPrivateProxies") ?? true;
            ForwardedHeadersKnownProxies.Configure(
                options,
                knownProxies,
                trustPrivateProxies,
                environment.IsDevelopment());
        });

        services.AddK7RateLimiting();

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        });
        authenticationBuilder.AddIdentityCookies();
        authenticationBuilder.AddScheme<AuthenticationSchemeOptions, EphemeralStreamTokenAuthenticationHandler>(
            EphemeralStreamTokenDefaults.AuthenticationScheme, _ => { });
        authenticationBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.AuthenticationScheme, null);

        services.ConfigureApplicationCookie(options =>
            ApplicationCookieOptions.Configure(options, cookieSecurePolicy));
        ApplicationCookieOptions.ConfigureTwoFactorCookies(services, cookieSecurePolicy);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.GuestOrAbove, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.AuthenticationSchemes.Add(ApiKeyAuthenticationDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.Guest, Roles.User, Roles.Administrator);
            });

            options.AddPolicy(Policies.UserOrAbove, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.AuthenticationSchemes.Add(ApiKeyAuthenticationDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.User, Roles.Administrator);
            });

            options.AddPolicy(Policies.AdminOnly, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.AuthenticationSchemes.Add(ApiKeyAuthenticationDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.Administrator);
            });

            options.AddPolicy(Policies.StreamAccess, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.AuthenticationSchemes.Add(EphemeralStreamTokenDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.Guest, Roles.User, Roles.Administrator);
            });

            options.AddPolicy(Policies.PeerAccess, policy =>
            {
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireClaim("scope", Domain.Constants.FederationScopes.Peer);
            });
        });

        services.AddDatabaseDeveloperPageExceptionFilter();
        services.AddScoped<IUser, CurrentUser>();
        services.AddSingleton<IPlaybackProgressNotifier, PlaybackProgressNotifier>();
        services.AddSingleton<MediaNotificationBatcher>();
        services.AddSingleton<ILibraryNotifier, LibraryNotifier>();
        services.AddSingleton<IBackgroundTaskNotifier, BackgroundTaskNotifier>();
        services.AddSingleton<IFederationNotifier, FederationNotifier>();
        services.AddSingleton<IClientErrorReporter, ServerSideErrorReporter>();
        services.AddHostedService<AdminStreamNotifier>();
        services.AddHostedService<ServerMetricsWarmupService>();
        services.AddHostedService<AdminMetricsNotifier>();
        services.AddHostedService<EphemeralStreamTokenCleanupService>();
        services.AddHostedService<StreamSessionCleanupService>();

        services.AddScoped<K7SnackbarService>();
        services.AddScoped<IK7Snackbar>(sp => sp.GetRequiredService<K7SnackbarService>());
        services.AddSignalR();
        services.AddHttpContextAccessor();
        services.AddScoped<IdentityUserAccessor>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>();

        services.AddExceptionHandler<CustomExceptionHandler>();
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents()
            .AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddScoped<ThemeService>();

        // Customise default API behaviour
        services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);

        services.AddEndpointsApiExplorer();

        services.ConfigureHttpJsonOptions(x => K7JsonSerializerOptions.Configure(x.SerializerOptions));

        return services;
    }

    public static IServiceCollection ConfigureCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var origins = configuration.GetSection("Cors:Origins").Get<string[]>();

                if (origins is { Length: > 0 })
                {
                    policy.WithOrigins(origins);
                }
                else if (environment.IsDevelopment())
                {
                    policy.SetIsOriginAllowed(origin =>
                    {
                        if (string.IsNullOrWhiteSpace(origin))
                            return false;

                        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback;
                    });
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => false);
                }

                policy.AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        return services;
    }

    public static void ConfigureSerilog(this IConfiguration configuration)
    {
        var configuredLogDirectory = configuration.GetValue<string>("Paths:Logs")!;
        var logFilePath = Path.Combine(configuredLogDirectory, "log-.log");
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(logFilePath, rollOnFileSizeLimit: true, fileSizeLimitBytes: 1000000, rollingInterval: RollingInterval.Day)
            .WriteTo.Console();

        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            loggerConfig.WriteTo.OpenTelemetry();
        }

        Log.Logger = loggerConfig.CreateLogger();
    }
}
