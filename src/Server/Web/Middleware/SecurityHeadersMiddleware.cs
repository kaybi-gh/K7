using K7.Server.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Middleware;

public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<AuthenticationConfiguration> authOptions,
    IWebHostEnvironment environment)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] = BuildContentSecurityPolicy(authOptions.Value, environment);

        return next(context);
    }

    internal static string BuildContentSecurityPolicy(
        AuthenticationConfiguration auth,
        IWebHostEnvironment environment)
    {
        var scriptSources = new List<string> { "'self'", "'wasm-unsafe-eval'", "'unsafe-inline'", "https://www.gstatic.com" };
        var connectSources = new List<string> { "'self'", "ws:", "wss:" };
        var formActions = new List<string> { "'self'" };

        if (auth.Oidc.Enabled
            && Uri.TryCreate(auth.Oidc.Authority, UriKind.Absolute, out var authorityUri))
        {
            var origin = authorityUri.GetLeftPart(UriPartial.Authority);
            connectSources.Add(origin);
            formActions.Add(origin);
        }

        if (environment.IsDevelopment())
        {
            connectSources.Add("http://localhost:*");
            connectSources.Add("https://localhost:*");
            connectSources.Add("ws://localhost:*");
            connectSources.Add("wss://localhost:*");
        }

        return string.Join("; ",
            "default-src 'self'",
            $"script-src {string.Join(' ', scriptSources)}",
            "script-src-attr 'unsafe-inline'",
            "worker-src 'self' blob:",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob: https://image.tmdb.org https://artworks.thetvdb.com https://coverartarchive.org https://*.archive.org",
            "font-src 'self' data:",
            $"connect-src {string.Join(' ', connectSources)}",
            "media-src 'self' blob:",
            "frame-src https://www.youtube-nocookie.com https://www.youtube.com https://player.vimeo.com",
            "frame-ancestors 'none'",
            "base-uri 'self'",
            $"form-action {string.Join(' ', formActions)}");
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
