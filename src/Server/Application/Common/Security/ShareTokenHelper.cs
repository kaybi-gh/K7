using System.Security.Cryptography;
using System.Text;

namespace K7.Server.Application.Common.Security;

/// <summary>
/// Share link tokens and visitor session tokens. Both are 256 random bits, so a plain
/// SHA-256 is enough to store them: only the hash is persisted.
/// </summary>
public static class ShareTokenHelper
{
    public const int MaxTokenLength = 128;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool LooksValid(string? token) =>
        !string.IsNullOrWhiteSpace(token) && token.Length <= MaxTokenLength;
}
