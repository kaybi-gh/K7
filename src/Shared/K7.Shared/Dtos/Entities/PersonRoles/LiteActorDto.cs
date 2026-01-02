namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record LiteActorDto : LitePersonRoleDto
{
    public string? CharacterName { get; init; }
}
