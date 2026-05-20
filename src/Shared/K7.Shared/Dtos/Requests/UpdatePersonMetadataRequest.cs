using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record UpdatePersonMetadataRequest
{
    public IList<string> LockedFields { get; init; } = [];
    public string? Name { get; init; }
    public PersonGender? Gender { get; init; }
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
}
