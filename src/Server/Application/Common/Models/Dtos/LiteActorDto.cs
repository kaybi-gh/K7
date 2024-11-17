namespace K7.Server.Application.Common.Models.Dtos;

public record LiteActorDto : LitePersonRoleDto
{
    public string? CharacterName { get; init; }
}
