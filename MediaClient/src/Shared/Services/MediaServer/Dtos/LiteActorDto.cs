namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record LiteActorDto : LitePersonRoleDto
{
    public string? CharacterName { get; init; }
}
