using System.Security.Claims;
using K7.Server.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Infrastructure;

internal static class ApplicationCookieOptions
{
    internal const string OidcSessionMarkerKey = ".oidc_session";
    internal const string OidcSessionMarkerValue = "1";

    internal static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);
    internal static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(30);

    internal static void Configure(CookieAuthenticationOptions options, CookieSecurePolicy cookieSecurePolicy)
    {
        options.LoginPath = "/welcome";
        options.LogoutPath = "/account/logout";
        ConfigureCookie(options, cookieSecurePolicy);
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = RememberMeLifetime;

        options.Events.OnSigningIn = context =>
        {
            MarkOidcSession(context);
            ApplySigningLifetime(context);
            return Task.CompletedTask;
        };

        options.Events.OnCheckSlidingExpiration = context =>
        {
            var lifetime = GetTicketLifetime(context.Properties, context.HttpContext, context.Principal);
            context.ShouldRenew = context.RemainingTime < lifetime / 2;
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;

        options.Events.OnRedirectToLogin = context =>
        {
            if (IsApiOrHubPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (IsApiOrHubPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    }

    private static void MarkOidcSession(CookieSigningInContext context)
    {
        var fromCallback = context.HttpContext.Items.ContainsKey(OidcSessionMarkerKey);
        var fromClaim = context.Principal?.HasClaim(ClaimTypes.AuthenticationMethod, "oidc") == true;
        if (!fromCallback && !fromClaim)
            return;

        context.Properties.Items[OidcSessionMarkerKey] = OidcSessionMarkerValue;
        context.Properties.IsPersistent = true;
    }

    private static void ApplySigningLifetime(CookieSigningInContext context)
    {
        // Ignore provisional ExpiresUtc set from ExpireTimeSpan before this event.
        var lifetime = ResolveConfiguredLifetime(context.Properties, context.HttpContext, context.Principal);
        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(lifetime);
    }

    private static TimeSpan GetTicketLifetime(
        AuthenticationProperties properties,
        HttpContext httpContext,
        ClaimsPrincipal? principal)
    {
        if (properties.IssuedUtc is { } issued && properties.ExpiresUtc is { } expires)
        {
            var span = expires - issued;
            if (span > TimeSpan.Zero)
                return span;
        }

        return ResolveConfiguredLifetime(properties, httpContext, principal);
    }

    private static TimeSpan ResolveConfiguredLifetime(
        AuthenticationProperties properties,
        HttpContext httpContext,
        ClaimsPrincipal? principal)
    {
        var isOidc = properties.Items.TryGetValue(OidcSessionMarkerKey, out var marker)
            && marker == OidcSessionMarkerValue;
        isOidc = isOidc
            || principal?.HasClaim(ClaimTypes.AuthenticationMethod, "oidc") == true;

        if (isOidc)
        {
            var oidcLifetime = httpContext.RequestServices
                .GetService<IOptions<AuthenticationConfiguration>>()
                ?.Value.Oidc.WebSessionLifetime;
            if (oidcLifetime is { } configured && configured > TimeSpan.Zero)
                return configured;

            return TimeSpan.FromDays(7);
        }

        return properties.IsPersistent ? RememberMeLifetime : SessionLifetime;
    }

    private static bool IsApiOrHubPath(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/hub");

    internal static void ConfigureTwoFactorCookies(
        IServiceCollection services,
        CookieSecurePolicy cookieSecurePolicy)
    {
        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme, options =>
            ConfigureCookie(options, cookieSecurePolicy));

        services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme, options =>
            ConfigureCookie(options, cookieSecurePolicy));
    }

    private static void ConfigureCookie(
        CookieAuthenticationOptions options,
        CookieSecurePolicy cookieSecurePolicy)
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Cookie.IsEssential = true;
    }
}
