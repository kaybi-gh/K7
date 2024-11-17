namespace K7.Clients.Shared.Domain.Models;

public record Actor : PersonRole
{
    public string? CharacterName { get; init; }
}

