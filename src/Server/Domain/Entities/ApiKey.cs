using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities;

public class ApiKey : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string KeyHash { get; set; }
    public required string KeyPrefix { get; set; }
    public ApiKeyScope Scope { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
