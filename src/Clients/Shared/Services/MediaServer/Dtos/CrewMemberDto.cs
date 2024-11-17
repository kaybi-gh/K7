namespace K7.Clients.Shared.Services.MediaServer.Dtos;

public record CrewMemberDto : PersonRoleDto
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}
