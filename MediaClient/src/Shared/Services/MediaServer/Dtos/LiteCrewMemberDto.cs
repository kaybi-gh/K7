namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record LiteCrewMemberDto : LitePersonRoleDto
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}
