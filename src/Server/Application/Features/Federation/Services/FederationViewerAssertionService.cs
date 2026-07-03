using System.Security.Cryptography;
using System.Text;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Services;

public interface IFederationViewerAssertionService
{
    string CreateAssertion(FederatedUserRef viewer, string signingSecret, TimeSpan? lifetime = null);
    FederatedUserRef? ValidateAssertion(string? assertion, string signingSecret);
}

public class FederationViewerAssertionService : IFederationViewerAssertionService
{
    private const char Separator = '|';

    public string CreateAssertion(FederatedUserRef viewer, string signingSecret, TimeSpan? lifetime = null)
    {
        var expires = DateTimeOffset.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)).ToUnixTimeSeconds();
        var displayName = viewer.DisplayName ?? string.Empty;
        var peerId = viewer.OriginPeerServerId?.ToString() ?? string.Empty;
        var payload = string.Join(Separator, viewer.OriginUserId, peerId, displayName, expires);
        var signature = ComputeSignature(payload, signingSecret);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}{Separator}{signature}"));
    }

    public FederatedUserRef? ValidateAssertion(string? assertion, string signingSecret)
    {
        if (string.IsNullOrWhiteSpace(assertion))
            return null;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(assertion));
            var lastSeparator = decoded.LastIndexOf(Separator);
            if (lastSeparator <= 0)
                return null;

            var payload = decoded[..lastSeparator];
            var signature = decoded[(lastSeparator + 1)..];
            if (!string.Equals(signature, ComputeSignature(payload, signingSecret), StringComparison.Ordinal))
                return null;

            var parts = payload.Split(Separator);
            if (parts.Length != 4)
                return null;

            if (!Guid.TryParse(parts[0], out var originUserId))
                return null;

            if (!long.TryParse(parts[3], out var expiresUnix))
                return null;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
                return null;

            Guid? peerId = Guid.TryParse(parts[1], out var parsedPeerId) ? parsedPeerId : null;

            return new FederatedUserRef
            {
                OriginUserId = originUserId,
                OriginPeerServerId = peerId == Guid.Empty ? null : peerId,
                DisplayName = string.IsNullOrWhiteSpace(parts[2]) ? null : parts[2]
            };
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeSignature(string payload, string signingSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
