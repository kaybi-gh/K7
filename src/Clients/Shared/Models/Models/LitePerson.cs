using K7.Clients.Shared.Domain.Enums;

namespace K7.Clients.Shared.Domain.Models;

public record LitePerson
{
    public string Id { get; init; } = null!;
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    public PersonGender? Gender { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public string? PortraitPictureHref { get; init; }
}
