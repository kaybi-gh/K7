using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Web.Infrastructure;

/// <summary>
/// Part of the stream policy. Regular accounts pass untouched. A share visitor only
/// reaches a stream endpoint when the file in the route belongs to one of its shares
/// and that share allows playback. Every stream endpoint is covered by construction:
/// a route without a file id is refused for visitors.
/// </summary>
public sealed class ShareStreamScopeRequirement : IAuthorizationRequirement;

public sealed class ShareStreamScopeHandler(
    IHttpContextAccessor httpContextAccessor,
    IShareVisitorContext visitorContext,
    IShareVisitorAccess shareVisitorAccess,
    IMediaAccessGuard accessGuard,
    IMemoryCache cache,
    IShareAccessInvalidator invalidator)
    : AuthorizationHandler<ShareStreamScopeRequirement>
{
    public const string FileRouteParameter = "id";

    // Players ask for many segments per minute: do not resolve the scope each time.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ShareStreamScopeRequirement requirement)
    {
        if (!visitorContext.IsShareVisitor)
        {
            context.Succeed(requirement);
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null
            || !Guid.TryParse(httpContext.Request.RouteValues[FileRouteParameter]?.ToString(), out var indexedFileId))
        {
            return;
        }

        var sessions = string.Join(',', visitorContext.Grants.Select(g => g.SessionId.ToString("N")).Order());
        var allowed = await cache.GetOrCreateAsync($"share:stream:{invalidator.Version}:{sessions}:{indexedFileId:N}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return await CanPlayAsync(indexedFileId, httpContext.RequestAborted);
        });

        if (allowed)
            context.Succeed(requirement);
    }

    private async Task<bool> CanPlayAsync(Guid indexedFileId, CancellationToken cancellationToken)
    {
        var matches = await shareVisitorAccess.GetMatchingSharesByIndexedFileAsync(indexedFileId, cancellationToken);
        if (!matches.Any(m => m.AllowPlayback))
            return false;

        try
        {
            await accessGuard.EnsureAccessByIndexedFileAsync(indexedFileId, cancellationToken);
            return true;
        }
        catch (Ardalis.GuardClauses.NotFoundException)
        {
            return false;
        }
    }
}
