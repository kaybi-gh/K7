namespace MediaClient.Shared.Domain.Models;

public record LiteActor : LitePersonRole
{
    public string? CharacterName { get; init; }
}

