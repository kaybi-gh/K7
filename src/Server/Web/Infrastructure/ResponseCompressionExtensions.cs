using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace K7.Server.Web.Infrastructure;

/// <summary>
/// Brotli / gzip for API JSON reads. Static assets are already served precompressed by
/// <c>MapStaticAssets</c>; media, images and HLS are binary and stay uncompressed.
/// </summary>
public static class ResponseCompressionExtensions
{
    // Responses that may carry a credential stay uncompressed (BREACH): API keys, app
    // passwords, authentication endpoints. Writes (POST/PUT/DELETE) are never compressed.
    private static readonly string[] ExcludedPathFragments =
    [
        "api-keys",
        "app-passwords",
        "/authentication",
        "ephemeral-token"
    ];

    public static IServiceCollection AddK7ResponseCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Bearer tokens travel in headers, never in the compressed bodies selected below.
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
            options.MimeTypes = ["application/json", "application/problem+json"];
        });

        // Fastest keeps the CPU cost negligible on small ARM hosts; JSON still shrinks 70-85 %.
        services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
        return services;
    }

    public static IApplicationBuilder UseK7ResponseCompression(this IApplicationBuilder app) =>
        app.UseWhen(ShouldCompress, branch => branch.UseResponseCompression());

    internal static bool ShouldCompress(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && !string.Equals(request.Method, HttpMethods.Query, StringComparison.OrdinalIgnoreCase))
            return false;

        var path = request.Path.Value;
        if (path is null || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var fragment in ExcludedPathFragments)
        {
            if (path.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
