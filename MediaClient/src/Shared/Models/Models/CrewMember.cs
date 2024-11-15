namespace MediaClient.Shared.Domain.Models;

public record CrewMember : PersonRole
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}

