namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record CrewMemberDto : PersonRoleDto
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}
