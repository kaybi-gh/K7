namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserRoleRequest
{
    public required string Role { get; init; }
}
