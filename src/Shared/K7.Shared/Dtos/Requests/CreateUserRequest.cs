namespace K7.Shared.Dtos.Requests;

public sealed record CreateUserRequest
{
    public required string Username { get; init; }
    public required string Role { get; init; }
    public string? Password { get; init; }
}
