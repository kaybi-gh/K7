using System.Security.Cryptography;
using System.Text;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Services;

public static class FederationCallbackProof
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private const long MaxSkewSeconds = 60;

    public static ProviderRevokeRequest CreateProviderRevoke(
        string providerUrl,
        string secret,
        DateTimeOffset? now = null)
    {
        var normalized = Normalize(providerUrl);
        var expires = (now ?? DateTimeOffset.UtcNow).Add(Lifetime).ToUnixTimeSeconds();
        return new ProviderRevokeRequest(normalized, expires, Sign(normalized, expires, secret));
    }

    public static bool VerifyProviderRevoke(
        ProviderRevokeRequest request,
        string? secret,
        DateTimeOffset? now = null)
    {
        var clock = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var lifetime = (long)Lifetime.TotalSeconds;
        var expiryOk = request.Expires >= clock && request.Expires <= clock + lifetime + MaxSkewSeconds;
        var expected = Sign(Normalize(request.ProviderUrl), request.Expires, secret ?? string.Empty);
        var match = FixedEquals(expected, request.Signature ?? string.Empty);
        return expiryOk && match && !string.IsNullOrEmpty(secret);
    }

    public static string Normalize(string url) => url.Trim().TrimEnd('/');

    private static string Sign(string providerUrl, long expiresUnix, string secret)
    {
        var payload = $"provider-revoke|{providerUrl}|{expiresUnix}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
