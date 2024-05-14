namespace MediaServer.Application.Common.Models.Dtos;

public record ActorDto : PersonRoleDto
{
    public string? CharacterName { get; init; }
}
