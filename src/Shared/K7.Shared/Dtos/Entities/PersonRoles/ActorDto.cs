namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record ActorDto : PersonRoleDto
{
    public string? CharacterName { get; init; }
}
