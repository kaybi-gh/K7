namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record LiteCrewMemberDto : LitePersonRoleDto
{
    public string? Department { get; init; }
    public string? Job { get; init; }
}
