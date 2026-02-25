using System.Text.Json.Serialization;
using K7.Clients.Shared.Services;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Web.Services;
using Microsoft.AspNetCore.Mvc;
using MudBlazor.Services;
using Serilog;
using K7.Server.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using K7.Server.Infrastructure.Database.Context.Data;

namespace K7.Server.Web;

public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
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
        authenticationBuilder.AddJwtBearer();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        });

        services.AddAuthorization();

        services.AddDatabaseDeveloperPageExceptionFilter();
        services.AddScoped<IUser, CurrentUser>();
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
            .AddAuthenticationStateSerialization();
        services.AddMudServices();
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

    public static IServiceCollection ConfigureCors(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                // policy.WithOrigins("http://fqdn"); // TODO
                //policy.SetIsOriginAllowed(origin => new Uri(origin).IsLoopback);
                policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("*");
            });
        });

        return services;
    }

    public static void ConfigureSerilog(this IConfiguration configuration)
    {
        var configuredLogDirectory = configuration.GetValue<string>("Paths:Logs")!;
        var logFilePath = Path.Combine(configuredLogDirectory, "log-.log");
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.File(logFilePath, rollOnFileSizeLimit: true, fileSizeLimitBytes: 1000000, rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();
    }
}
