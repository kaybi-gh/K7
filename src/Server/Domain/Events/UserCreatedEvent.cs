using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Events;

public class UserCreatedEvent(
    Guid userId,
    string? userName,
    string? email,
    string? role,
    UserCreationOrigin origin) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public string? Email { get; } = email;
    public string? Role { get; } = role;
    public UserCreationOrigin Origin { get; } = origin;
}
