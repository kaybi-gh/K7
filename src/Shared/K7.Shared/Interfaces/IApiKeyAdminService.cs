using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IApiKeyAdminService
{
    Task<List<ApiKeyDto>> GetApiKeysAsync(CancellationToken cancellationToken = default);
    Task<CreateApiKeyResponse> CreateApiKeyAsync(string name, ApiKeyScope scope, DateTime? expiresAt = null, CancellationToken cancellationToken = default);
    Task RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default);
}
