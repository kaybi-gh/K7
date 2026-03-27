namespace K7.Shared.Dtos.Users;

public sealed record LiteUserDto
{
    public Guid Id { get; init; }
    public string? IdentityUserId { get; init; }
}
