namespace K7.Shared.Dtos;

public sealed record OnlineUsersPresenceDto
{
    public required IReadOnlyList<string> IdentityUserIds { get; init; }
}
