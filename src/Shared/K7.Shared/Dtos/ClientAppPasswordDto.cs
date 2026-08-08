namespace K7.Shared.Dtos;

public sealed record ClientAppPasswordDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
}

public sealed record CreateClientAppPasswordResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Password { get; init; }
}
