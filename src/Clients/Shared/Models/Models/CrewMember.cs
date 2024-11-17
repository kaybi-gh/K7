namespace K7.Clients.Shared.Domain.Models;

public record CrewMember : PersonRole
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}

