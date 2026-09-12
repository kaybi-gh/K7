namespace K7.Server.Domain.Events;

public class UserDeletedEvent(
    Guid userId,
    string? userName,
    string? email,
    string? role) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public string? Email { get; } = email;
    public string? Role { get; } = role;
}
