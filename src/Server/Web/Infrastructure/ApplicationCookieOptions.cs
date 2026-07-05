using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Web.Infrastructure;

internal static class ApplicationCookieOptions
{
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
            ApplyLifetime(context.Properties);
            return Task.CompletedTask;
        };

        options.Events.OnCheckSlidingExpiration = context =>
        {
            var lifetime = GetLifetime(context.Properties);
            context.ShouldRenew = context.RemainingTime < lifetime / 2;
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = context =>
        {
            if (context.ShouldRenew)
            {
                ApplyLifetime(context.Properties);
            }

            return Task.CompletedTask;
        };
    }

    private static TimeSpan GetLifetime(AuthenticationProperties properties) =>
        properties.IsPersistent ? RememberMeLifetime : SessionLifetime;

    private static void ApplyLifetime(AuthenticationProperties properties) =>
        properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(GetLifetime(properties));

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
