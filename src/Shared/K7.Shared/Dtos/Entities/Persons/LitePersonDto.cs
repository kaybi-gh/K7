using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Shared.Dtos.Entities.Persons;

public sealed record LitePersonDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }

}
