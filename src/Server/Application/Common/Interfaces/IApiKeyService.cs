using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Interfaces;

public interface IApiKeyService
{
    (string fullKey, string keyHash, string keyPrefix) GenerateKey();
    string HashKey(string fullKey);
    Task<ApiKey?> ValidateKeyAsync(string fullKey, CancellationToken cancellationToken = default);
}
