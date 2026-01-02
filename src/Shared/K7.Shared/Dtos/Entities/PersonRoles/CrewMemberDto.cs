namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record CrewMemberDto : PersonRoleDto
{
    public string? Department { get; init; }
    public string? Job { get; init; }
}
