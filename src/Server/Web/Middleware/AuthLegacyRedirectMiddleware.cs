using Microsoft.AspNetCore.Http;

namespace K7.Server.Web.Middleware;

public class AuthLegacyRedirectMiddleware(RequestDelegate next)
{
    private static readonly Dictionary<string, string> PathRedirects = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/account/login"] = "/sign-in",
        ["/sign-in/local"] = "/sign-in",
        ["/account/register"] = "/sign-up",
        ["/account/loginwith2fa"] = "/sign-in/two-factor",
        ["/account/loginwithrecoverycode"] = "/sign-in/recovery-code",
        ["/account/lockout"] = "/sign-in/lockout",
        ["/account/accessdenied"] = "/sign-in/access-denied",
        ["/link"] = "/link-device/authorize",
    };

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (PathRedirects.TryGetValue(path, out var target))
        {
            var query = context.Request.QueryString.Value ?? string.Empty;
            context.Response.Redirect(target + query);
            return Task.CompletedTask;
        }

        return next(context);
    }
}

public static class AuthLegacyRedirectMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthLegacyRedirects(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthLegacyRedirectMiddleware>();
    }
}
