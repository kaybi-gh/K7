using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record ApiKeyDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string KeyPrefix { get; init; }
    public ApiKeyScope Scope { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public sealed record CreateApiKeyResponse
{
    public Guid Id { get; init; }
    public required string FullKey { get; init; }
}
