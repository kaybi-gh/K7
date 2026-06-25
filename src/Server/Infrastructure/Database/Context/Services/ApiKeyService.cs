using System.Security.Cryptography;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.Database.Context.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly IApplicationDbContext _context;

    public ApiKeyService(IApplicationDbContext context)
    {
        _context = context;
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
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fullKey));
        return Convert.ToHexStringLower(hash);
    }

    public async Task<ValidatedApiKey?> ValidateKeyAsync(string fullKey, CancellationToken cancellationToken = default)
    {
        var hash = HashKey(fullKey);
        var match = await (
            from key in _context.ApiKeys.AsNoTracking()
            join user in _context.Users.AsNoTracking() on key.CreatedByUserId equals user.Id
            where key.KeyHash == hash
            select new { Key = key, user.IdentityUserId }
        ).FirstOrDefaultAsync(cancellationToken);

        if (match is null)
            return null;

        if (match.Key.ExpiresAt.HasValue && match.Key.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        if (string.IsNullOrEmpty(match.IdentityUserId))
            return null;

        await _context.ApiKeys
            .Where(k => k.Id == match.Key.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(k => k.LastUsedAt, DateTime.UtcNow),
                cancellationToken);

        return new ValidatedApiKey(match.Key, match.IdentityUserId);
    }
}
