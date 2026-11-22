namespace K7.Shared.Dtos.Users;

/// <summary>
/// An account the current user is a guardian of, and may open a device session for.
/// </summary>
public sealed record ManagedAccountDto
{
    public required Guid Id { get; init; }
    public string? UserName { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public required bool HasPin { get; init; }
}
