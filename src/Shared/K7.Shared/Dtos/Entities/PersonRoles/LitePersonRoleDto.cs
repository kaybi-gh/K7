using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Shared.Dtos.Entities.Persons;

namespace K7.Shared.Dtos.Entities.PersonRoles;

[JsonDerivedType(typeof(LiteActorDto), nameof(Actor))]
[JsonDerivedType(typeof(LiteCrewMemberDto), nameof(CrewMember))]
[JsonDerivedType(typeof(LiteMusicArtistRoleDto), nameof(MusicArtist))]
public abstract record LitePersonRoleDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int? Order { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }
    public LitePersonDto? Person { get; init; }
    public IList<ExternalIdDto> ExternalIds { get; init; } = [];

    public static LitePersonRoleDto FromDomain(BasePersonRole domain) => domain switch
    {
        Actor actor => new LiteActorDto()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null,
            Person = LitePersonDto.FromDomain(domain.Person),
            CharacterName = actor.CharacterName
        },
        CrewMember crewMember => new LiteCrewMemberDto()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null,
            Person = LitePersonDto.FromDomain(domain.Person),
            Department = crewMember.Department,
            Job = crewMember.Department
        },
        MusicArtist musicArtist => new LiteMusicArtistRoleDto()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null,
            Person = LitePersonDto.FromDomain(domain.Person),
            IsGuest = musicArtist.IsGuest
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
