namespace K7.Clients.Shared.Models;

public record CrewMember : PersonRole
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}

