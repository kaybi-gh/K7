namespace K7.Server.Domain.Events;

public class ClientAppPasswordCreatedEvent(
    Guid clientAppPasswordId,
    string name,
    Guid userId,
    string? userName) : BaseEvent
{
    public Guid ClientAppPasswordId { get; } = clientAppPasswordId;
    public string Name { get; } = name;
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
}
