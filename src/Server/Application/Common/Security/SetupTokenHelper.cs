using System.Security.Cryptography;
using System.Text;

namespace K7.Server.Application.Common.Security;

public static class SetupTokenHelper
{
    public static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash);
    }

    public static bool VerifyToken(string? providedToken, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(providedToken) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        var providedHash = HashToken(providedToken);
        var providedBytes = Convert.FromHexString(providedHash);
        var storedBytes = Convert.FromHexString(storedHash);
        return CryptographicOperations.FixedTimeEquals(providedBytes, storedBytes);
    }
}
