using System.Security.Cryptography;

namespace K7.Clients.Shared.Services;

public static class PinVerifier
{
    public static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, 10_000, HashAlgorithmName.SHA256, 32);
        return $"$PBKDF2$iterations=10000${Convert.ToHexStringLower(salt)}${Convert.ToHexStringLower(hash)}";
    }

    public static bool Verify(string? storedHash, string pin)
    {
        if (storedHash is null)
            return true;

        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "PBKDF2")
            return false;

        var iterParts = parts[1].Split('=');
        if (iterParts.Length != 2 || !int.TryParse(iterParts[1], out var iterations))
            return false;

        var salt = Convert.FromHexString(parts[2]);
        var expectedHash = Convert.FromHexString(parts[3]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
