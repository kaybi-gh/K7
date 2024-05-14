namespace MediaServer.Application.Common.Models.Dtos;

public record LiteCrewMemberDto : LitePersonRoleDto
{
    public string? Department { get; init; }
    public string? Job { get; init; }
}
