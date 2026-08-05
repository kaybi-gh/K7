using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class ClientAppPasswordService(IDataProtectionProvider dataProtectionProvider) : IClientAppPasswordService
{
    private const string ProtectedPrefix = "k7dp:";

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("K7.ClientAppPasswords.v1");

    private readonly PasswordHasher<object> _legacyHasher = new();

    public string HashPassword(string password)
    {
        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(password));
        return ProtectedPrefix + Convert.ToBase64String(protectedBytes);
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        if (TryUnprotect(passwordHash, out var plaintext))
            return FixedTimeEqualsUtf8(plaintext, password);

        // Legacy Identity PasswordHasher values created before encrypted storage.
        var result = _legacyHasher.VerifyHashedPassword(null!, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    public bool VerifyToken(string passwordHash, string token, string salt)
    {
        if (!TryUnprotect(passwordHash, out var plaintext))
            return false;

        // Subsonic token = md5(password + salt) as lowercase hex (protocol requirement).
#pragma warning disable CA5351
        var expected = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(plaintext + salt)))
            .ToLowerInvariant();
#pragma warning restore CA5351
        return FixedTimeEqualsUtf8(expected, token.Trim().ToLowerInvariant());
    }

    public (string password, string hash) GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        var password = Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
        var hash = HashPassword(password);
        return (password, hash);
    }

    private bool TryUnprotect(string passwordHash, out string plaintext)
    {
        plaintext = string.Empty;
        if (!passwordHash.StartsWith(ProtectedPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            var protectedBytes = Convert.FromBase64String(passwordHash[ProtectedPrefix.Length..]);
            plaintext = Encoding.UTF8.GetString(_protector.Unprotect(protectedBytes));
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeEqualsUtf8(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        if (leftBytes.Length != rightBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
