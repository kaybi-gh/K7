using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace K7.Clients.MAUI.Services.Authentication;

/// <summary>
/// Best-effort reads of a stored access token. Older K7 servers issue OpenIddict JWEs (current ones sign
/// plain JWTs): for a JWE the handler parses the envelope but exposes no payload (ValidTo is DateTime.MinValue, no claims), so
/// callers fall back to the persisted expiry and claims snapshot (<c>StoredSession</c>).
/// </summary>
internal static class JwtAccessToken
{
    public static DateTimeOffset? TryReadExpiry(string? accessToken)
    {
        var jwt = TryRead(accessToken);
        if (jwt is null || jwt.ValidTo == DateTime.MinValue)
            return null;

        return new DateTimeOffset(DateTime.SpecifyKind(jwt.ValidTo, DateTimeKind.Utc));
    }

    public static IReadOnlyList<Claim> ReadClaims(string? accessToken) =>
        TryRead(accessToken)?.Claims.ToList() ?? [];

    private static JwtSecurityToken? TryRead(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return null;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            return handler.CanReadToken(accessToken) ? handler.ReadJwtToken(accessToken) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            return null;
        }
    }
}
