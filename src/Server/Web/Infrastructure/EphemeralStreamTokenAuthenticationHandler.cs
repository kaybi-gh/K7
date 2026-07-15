using System.Security.Claims;
using System.Text.Encodings.Web;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace K7.Server.Web.Infrastructure;

public static class EphemeralStreamTokenDefaults
{
    public const string AuthenticationScheme = "EphemeralStreamToken";
    public const string QueryParameterName = "ephemeral_token";
}

public class EphemeralStreamTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EphemeralStreamTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IServiceScopeFactory scopeFactory)
        : base(options, logger, encoder)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Query[EphemeralStreamTokenDefaults.QueryParameterName].FirstOrDefault();
        if (string.IsNullOrEmpty(token))
        {
            return AuthenticateResult.NoResult();
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var ephemeralToken = await context.EphemeralStreamTokens
            .AsNoTracking()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);

        if (ephemeralToken is null)
        {
            return AuthenticateResult.Fail("Invalid ephemeral stream token.");
        }

        if (!ephemeralToken.IsUsable(DateTimeOffset.UtcNow))
        {
            return AuthenticateResult.Fail(ephemeralToken.IsRevoked
                ? "Ephemeral stream token has been revoked."
                : "Ephemeral stream token has expired.");
        }

        var identityUserId = ephemeralToken.User.IdentityUserId;
        if (string.IsNullOrEmpty(identityUserId))
        {
            return AuthenticateResult.Fail("User has no identity.");
        }

        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var roles = await identityService.GetRolesAsync(identityUserId);
        var role = roles.FirstOrDefault() ?? Roles.Guest;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityUserId),
            new(ClaimTypes.Role, role),
            new("stream_session_id", ephemeralToken.StreamSessionId.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
