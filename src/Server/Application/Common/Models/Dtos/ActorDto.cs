namespace K7.Server.Application.Common.Models.Dtos;

public record ActorDto : PersonRoleDto
{
    public string? CharacterName { get; init; }
}
