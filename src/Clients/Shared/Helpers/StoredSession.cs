using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Persisted session facts the native clients keep next to their tokens. Older K7 servers
/// issue OpenIddict JWEs (current ones sign plain JWTs): the client cannot rely on reading "exp" or claims, so the expiry from the
/// token response and the signed-in principal's claims are stored alongside the token and
/// drive "is the stored token still usable" and "restore without the token endpoint".
/// </summary>
public static class StoredSession
{
    /// <summary>Margin before expiry under which a stored access token is not reused.</summary>
    public static readonly TimeSpan ReuseSkew = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string FormatExpiry(DateTimeOffset expiresAt) =>
        expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset? ParseExpiry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>True when the token is known to stay valid for more than <see cref="ReuseSkew"/>.</summary>
    public static bool IsAccessTokenUsable(DateTimeOffset? expiresAt, DateTimeOffset now) =>
        expiresAt is { } value && value > now + ReuseSkew;

    /// <summary>
    /// True when the token expires within <paramref name="skew"/>. An unknown expiry counts as
    /// near expiry so a token stored by an older build is refreshed once and dated.
    /// </summary>
    public static bool IsAccessTokenNearExpiry(DateTimeOffset? expiresAt, DateTimeOffset now, TimeSpan skew) =>
        expiresAt is not { } value || value <= now + skew;

    public static string SerializeClaims(IEnumerable<Claim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var stored = claims.Select(c => new StoredClaim(c.Type, c.Value)).ToList();
        return JsonSerializer.Serialize(stored, JsonOptions);
    }

    /// <summary>Empty on missing or corrupt input: callers fall back to a network refresh.</summary>
    public static IReadOnlyList<Claim> DeserializeClaims(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var stored = JsonSerializer.Deserialize<List<StoredClaim>>(json, JsonOptions);
            if (stored is null)
                return [];

            return stored
                .Where(c => !string.IsNullOrEmpty(c.Type) && c.Value is not null)
                .Select(c => new Claim(c.Type!, c.Value!))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record StoredClaim(string? Type, string? Value);
}
