namespace K7.Clients.Shared.Models;

public record Actor : PersonRole
{
    public string? CharacterName { get; init; }
}

