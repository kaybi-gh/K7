namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record LiteActorDto : LitePersonRoleDto
{
    public string? CharacterName { get; init; }
}
