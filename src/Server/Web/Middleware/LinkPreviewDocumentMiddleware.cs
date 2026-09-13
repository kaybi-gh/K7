using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Settings;
using K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;
using K7.Shared.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using AppComponent = K7.Server.Web.Components.App;

namespace K7.Server.Web.Middleware;

public sealed class LinkPreviewDocumentMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    IStringLocalizer<AppComponent> localizer,
    ILogger<LinkPreviewDocumentMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISender sender,
        IServerSettingsService serverSettings,
        IMemoryCache memoryCache)
    {
        if (!ShouldHandle(context))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "/";
        LinkPreview? preview = null;
        if (ShouldIncludeMediaPreview(path, await GetMediaLinkPreviewsEnabledAsync(serverSettings, memoryCache, context.RequestAborted)))
        {
            try
            {
                preview = await sender.Send(new GetLinkPreviewQuery(path), context.RequestAborted);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Link preview lookup failed for {Path}", path);
            }
        }

        var origin = LinkPreviewUrls.Origin(
            context.Request,
            configuration["BaseUrl"]?.TrimEnd('/') ?? "https://localhost:7443");
        var serverName = configuration["Server:Name"] is { Length: > 0 } name ? name : "K7";
        var html = LinkPreviewDocument.Render(
            origin,
            context.Request.Path,
            serverName,
            localizer["Description"],
            LinkPreviewUrls.ImageAsset,
            preview);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "public, max-age=300";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }

    internal static bool ShouldHandle(HttpContext context) =>
        HttpMethods.IsGet(context.Request.Method)
        && context.User.Identity?.IsAuthenticated != true
        && IsPreviewPath(context.Request.Path);

    internal static bool ShouldIncludeMediaPreview(string path, bool mediaLinkPreviewsEnabled) =>
        mediaLinkPreviewsEnabled && LinkPreviewRoute.TryParse(path, out _);

    internal static bool IsPreviewPath(PathString path)
    {
        var value = path.Value ?? "/";
        if (value == "/" || value.Length == 0)
            return true;

        return LinkPreviewRoute.TryParse(value, out _);
    }

    private static async Task<bool> GetMediaLinkPreviewsEnabledAsync(
        IServerSettingsService serverSettings,
        IMemoryCache memoryCache,
        CancellationToken cancellationToken)
    {
        var flags = await memoryCache.GetOrCreateAsync(ServerFeatureFlagsCache.Key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ServerFeatureFlagsCache.Duration;
            return await serverSettings.GetFeatureFlagsAsync(cancellationToken);
        }) ?? new ServerFeatureFlagsDto();

        return flags.MediaLinkPreviewsEnabled;
    }
}

public static class LinkPreviewDocumentMiddlewareExtensions
{
    public static IApplicationBuilder UseLinkPreviewDocuments(this IApplicationBuilder app) =>
        app.UseMiddleware<LinkPreviewDocumentMiddleware>();
}
