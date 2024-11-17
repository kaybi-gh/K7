namespace K7.Clients.Shared.Domain.Models;

public record LiteActor : LitePersonRole
{
    public string? CharacterName { get; init; }
}

