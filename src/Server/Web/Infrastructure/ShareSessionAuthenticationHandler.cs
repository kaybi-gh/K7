using System.Security.Claims;
using System.Text.Encodings.Web;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Constants;
using K7.Server.Infrastructure.Database.Context.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Infrastructure;

public static class ShareSessionDefaults
{
    public const string AuthenticationScheme = "ShareSession";

    /// <summary>
    /// One claim per share the visitor opened, as "{shareId}:{sessionId}".
    /// </summary>
    public const string GrantClaimType = "k7_share";

    /// <summary>
    /// How long a validated cookie is trusted before the database is asked again.
    /// Revoking, editing or deleting a share takes effect at once (see
    /// <see cref="IShareAccessInvalidator"/>). Only expiration waits for this delay.
    /// </summary>
    public static readonly TimeSpan ValidationCacheDuration = TimeSpan.FromSeconds(15);
}

/// <summary>
/// The visitor session cookie. It holds one token per open share so several shares can be
/// open in the same browser. HttpOnly, and scoped to the API so pages never see it.
/// </summary>
public static class ShareSessionCookie
{
    public const string Name = "K7.Shares";
    private const char Separator = '.';
    private const int MaxSessions = 8;

    public static IReadOnlyList<string> Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(Name, out var value) || string.IsNullOrWhiteSpace(value))
            return [];

        return value
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(ShareTokenHelper.LooksValid)
            .Distinct()
            .Take(MaxSessions)
            .ToList();
    }

    /// <summary>
    /// Adds a session token to the cookie, newest first. The oldest ones fall off.
    /// </summary>
    public static void Add(HttpContext httpContext, string sessionToken)
    {
        var tokens = Read(httpContext.Request)
            .Where(token => token != sessionToken)
            .Prepend(sessionToken)
            .Take(MaxSessions);

        httpContext.Response.Cookies.Append(Name, string.Join(Separator, tokens), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            // Same rule as the account cookie: always Secure unless the server opted out.
            Secure = httpContext.Request.IsHttps
                || (httpContext.RequestServices.GetRequiredService<IConfiguration>()
                    .GetValue<bool?>("Security:ForceHttps") ?? true),
            SameSite = SameSiteMode.Lax,
            Path = "/api",
            MaxAge = TimeSpan.FromDays(1)
        });
    }
}

/// <summary>
/// Authenticates a public share visitor from its session cookie. The principal acts as the
/// technical share visitor account and carries one grant claim per open share.
/// Only the share policy and the stream policy list this scheme.
/// </summary>
public class ShareSessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    IShareAccessInvalidator invalidator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string VisitorIdentityCacheKey = "share:visitor-identity-id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tokens = ShareSessionCookie.Read(Request);
        if (tokens.Count == 0)
            return AuthenticateResult.NoResult();

        // The version changes whenever a share is revoked, edited or deleted, so a cached
        // decision is never used past that point. Refusals are not cached: random cookies
        // must not fill the cache.
        var cacheKey = $"share:grants:{invalidator.Version}:{ShareTokenHelper.Hash(string.Join('.', tokens))}";
        if (!cache.TryGetValue(cacheKey, out ValidatedShareVisitor? validated) || validated is null)
        {
            validated = await ValidateAsync(tokens);
            if (validated.Grants.Count > 0)
                cache.Set(cacheKey, validated, ShareSessionDefaults.ValidationCacheDuration);
        }

        if (validated is null || validated.Grants.Count == 0 || string.IsNullOrEmpty(validated.IdentityUserId))
            return AuthenticateResult.NoResult();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, validated.IdentityUserId),
            new(ClaimTypes.Role, Roles.ShareVisitor)
        };
        claims.AddRange(validated.Grants.Select(grant =>
            new Claim(ShareSessionDefaults.GrantClaimType, $"{grant.ShareId:N}:{grant.SessionId:N}")));

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    private async Task<ValidatedShareVisitor> ValidateAsync(IReadOnlyList<string> tokens)
    {
        using var scope = scopeFactory.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IShareSessionValidator>();
        var grants = await validator.ValidateAsync(tokens, Context.RequestAborted);
        if (grants.Count == 0)
            return new ValidatedShareVisitor(null, []);

        var identityUserId = await cache.GetOrCreateAsync(VisitorIdentityCacheKey, async entry =>
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var visitor = await userManager.FindByNameAsync(SystemUsers.ShareVisitorUserName);

            // Do not pin a missing account: it is seeded at startup.
            entry.AbsoluteExpirationRelativeToNow = visitor is null
                ? TimeSpan.FromSeconds(5)
                : TimeSpan.FromHours(1);
            return visitor?.Id;
        });

        return new ValidatedShareVisitor(identityUserId, grants);
    }

    private sealed record ValidatedShareVisitor(string? IdentityUserId, IReadOnlyList<ShareVisitorGrant> Grants);
}
