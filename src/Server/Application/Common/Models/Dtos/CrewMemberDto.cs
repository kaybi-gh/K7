namespace K7.Server.Application.Common.Models.Dtos;

public record CrewMemberDto : PersonRoleDto
{
    public string? Department { get; init; }
    public string? Job { get; init; }
}
