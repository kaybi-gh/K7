using System.Security.Cryptography;

namespace K7.Server.Application.Common.Security;

public static class PinHashHelper
{
    private const int DefaultIterations = 600_000;

    public static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, salt, DefaultIterations, HashAlgorithmName.SHA256, 32);
        return $"$PBKDF2$iterations={DefaultIterations}${Convert.ToHexStringLower(salt)}${Convert.ToHexStringLower(hash)}";
    }

    public static bool Verify(string storedHash, string pin)
    {
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
