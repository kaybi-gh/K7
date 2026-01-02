using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Shared.Dtos.Entities.Persons;

public sealed record LitePersonDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }

    public static LitePersonDto FromDomain(Person domain) => new()
    {
        Id = domain.Id,
        Slug = domain.Slug,
        Name = domain.Name,
        Gender = domain.Gender,
        Birthday = domain.Birthday,
        Deathday = domain.Deathday,
        BirthPlace = domain.BirthPlace,
        PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null
    };
}

