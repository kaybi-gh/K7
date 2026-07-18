using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly IApplicationDbContext _context;
    private readonly byte[] _hashSecretBytes;

    public ApiKeyService(IApplicationDbContext context, IOptions<SecurityConfiguration> security)
    {
        _context = context;
        var hashSecret = security.Value.ApiKeys.HashSecret;
        if (string.IsNullOrWhiteSpace(hashSecret))
        {
            throw new InvalidOperationException(
                "Security:ApiKeys:HashSecret is required. Set Security__ApiKeys__HashSecret (or Security__ApiKeys__HashSecret__File) before creating or validating API keys.");
        }

        _hashSecretBytes = Encoding.UTF8.GetBytes(hashSecret);
    }

    public (string fullKey, string keyHash, string keyPrefix) GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var fullKey = $"k7_{Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
        var keyHash = HashKey(fullKey);
        var keyPrefix = fullKey[..11];
        return (fullKey, keyHash, keyPrefix);
    }

    public string HashKey(string fullKey)
    {
        var hash = HMACSHA256.HashData(_hashSecretBytes, Encoding.UTF8.GetBytes(fullKey));
        return Convert.ToHexStringLower(hash);
    }

    public async Task<ValidatedApiKey?> ValidateKeyAsync(string fullKey, CancellationToken cancellationToken = default)
    {
        if (fullKey.Length < 11)
            return null;

        var prefix = fullKey[..11];
        var computedBytes = Convert.FromHexString(HashKey(fullKey));

        var candidates = await (
            from key in _context.ApiKeys
            join user in _context.Users on key.CreatedByUserId equals user.Id
            where key.KeyPrefix == prefix
            select new { Key = key, user.IdentityUserId }
        ).AsNoTracking().ToListAsync(cancellationToken);

        ValidatedApiKey? matched = null;
        foreach (var candidate in candidates)
        {
            byte[] storedBytes;
            try
            {
                storedBytes = Convert.FromHexString(candidate.Key.KeyHash);
            }
            catch (FormatException)
            {
                continue;
            }

            if (storedBytes.Length != computedBytes.Length)
                continue;

            if (!CryptographicOperations.FixedTimeEquals(storedBytes, computedBytes))
                continue;

            matched = new ValidatedApiKey(candidate.Key, candidate.IdentityUserId ?? string.Empty);
        }

        if (matched is null || string.IsNullOrEmpty(matched.IdentityUserId))
            return null;

        if (matched.Key.ExpiresAt.HasValue && matched.Key.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        await _context.ApiKeys
            .Where(k => k.Id == matched.Key.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(k => k.LastUsedAt, DateTime.UtcNow),
                cancellationToken);

        return matched;
    }
}
