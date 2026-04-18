using System.Text.Json.Serialization;
using K7.Server.Domain.Constants;
using K7.Clients.Shared.Services;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using K7.Server.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using K7.Server.Infrastructure.Database.Context.Data;
using OpenIddict.Validation.AspNetCore;

namespace K7.Server.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services, IConfiguration configuration)
    {
        var metadataPath = configuration.GetValue<string>("Paths:Metadatas") ?? "metadatas";
        var keysPath = Path.Combine(metadataPath, "dataprotection-keys");
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("K7");

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "__Host-X-XSRF-TOKEN";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        });
        authenticationBuilder.AddIdentityCookies();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/welcome";
            options.LogoutPath = "/Account/Logout";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.GuestOrAbove, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.Guest, Roles.User, Roles.Administrator);
            });

            options.AddPolicy(Policies.UserOrAbove, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.User, Roles.Administrator);
            });

            options.AddPolicy(Policies.AdminOnly, policy =>
            {
                policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
                policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                policy.RequireRole(Roles.Administrator);
            });
        });

        services.AddDatabaseDeveloperPageExceptionFilter();
        services.AddScoped<IUser, CurrentUser>();
        services.AddSingleton<IPlaybackProgressNotifier, PlaybackProgressNotifier>();
        services.AddSingleton<ILibraryNotifier, LibraryNotifier>();
        services.AddSingleton<IBackgroundTaskNotifier, BackgroundTaskNotifier>();
        services.AddSignalR();
        //services.AddHttpForwarderWithServiceDiscovery(); // TODO - To keep or not?
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

        services.ConfigureHttpJsonOptions(x =>
        {
            x.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            x.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            x.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        }); // TODO - Share jsonOptions between server and clients?

        return services;
    }

    public static IServiceCollection ConfigureCors(this IServiceCollection services, IConfiguration configuration)
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
                else
                {
                    policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback);
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
