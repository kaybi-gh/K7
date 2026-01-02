using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Shared.Dtos.Entities.Persons;

public sealed record PersonDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }

    public IEnumerable<PersonRoleDto> Roles { get; init; } = [];
    public IEnumerable<ExternalIdDto> ExternalIds { get; init; } = [];
    public MetadataPictureDto? PortraitPicture { get; init; }

    public static PersonDto FromDomain(Person domain) => new()
    {
        Id = domain.Id,
        Slug = domain.Slug,
        Name = domain.Name,
        Gender = domain.Gender,
        Biography = domain.Biography,
        Birthday = domain.Birthday,
        Deathday = domain.Deathday,
        BirthPlace = domain.BirthPlace,
        Roles = domain.Roles.Select(PersonRoleDto.FromDomain),
        ExternalIds = domain.ExternalIds.Select(ExternalIdDto.FromDomain),
        PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null
    };
}

