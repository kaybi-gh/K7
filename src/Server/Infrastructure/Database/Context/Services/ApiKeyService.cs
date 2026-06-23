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

    public async Task<ApiKey?> ValidateKeyAsync(string fullKey, CancellationToken cancellationToken = default)
    {
        var hash = HashKey(fullKey);
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);

        if (apiKey is null)
            return null;

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
            return null;

        apiKey.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return apiKey;
    }
}
