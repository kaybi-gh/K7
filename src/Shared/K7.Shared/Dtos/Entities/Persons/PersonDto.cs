using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Shared.Dtos.Entities.Persons;

public sealed record PersonDto
{
    public Guid Id { get; init; }
    public string? Name { get; init; }
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }

    public IReadOnlyList<PersonRoleDto> Roles { get; init; } = [];
    public IReadOnlyList<ExternalIdDto> ExternalIds { get; init; } = [];
    public MetadataPictureDto? PortraitPicture { get; init; }

}
