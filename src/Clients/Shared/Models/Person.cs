using K7.Clients.Shared.Enums;

namespace K7.Clients.Shared.Models;

public record Person
{
    public string Id { get; init; } = null!;
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    public PersonGender? Gender { get; init; }
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public string? PortraitPictureHref { get; init; }
    public List<LiteMedia>? Medias { get; init; }
}
