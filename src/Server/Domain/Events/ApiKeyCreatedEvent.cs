namespace K7.Server.Domain.Events;

public class ApiKeyCreatedEvent(
    Guid apiKeyId,
    string name,
    string scope,
    string keyPrefix,
    Guid createdByUserId,
    string? createdByUserName) : BaseEvent
{
    public Guid ApiKeyId { get; } = apiKeyId;
    public string Name { get; } = name;
    public string Scope { get; } = scope;
    public string KeyPrefix { get; } = keyPrefix;
    public Guid CreatedByUserId { get; } = createdByUserId;
    public string? CreatedByUserName { get; } = createdByUserName;
}
