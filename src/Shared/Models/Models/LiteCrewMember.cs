namespace MediaClient.Shared.Domain.Models;

public record LiteCrewMember : LitePersonRole
{
    public string? Department { get; set; }
    public string? Job { get; set; }
}

