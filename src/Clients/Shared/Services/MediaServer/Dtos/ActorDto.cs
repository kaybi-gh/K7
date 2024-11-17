namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record ActorDto : PersonRoleDto
{
    public string? CharacterName { get; init; }
}
