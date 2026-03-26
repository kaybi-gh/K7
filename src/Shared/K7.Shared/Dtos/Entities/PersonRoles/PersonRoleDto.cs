using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;

namespace K7.Shared.Dtos.Entities.PersonRoles;

[JsonDerivedType(typeof(ActorDto), nameof(Actor))]
[JsonDerivedType(typeof(CrewMemberDto), nameof(CrewMember))]
[JsonDerivedType(typeof(MusicArtistRoleDto), nameof(MusicArtist))]
public abstract record PersonRoleDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int? Order { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }
    public LiteMediaDto? Media { get; init; }
    public LitePersonDto? Person { get; init; }
    public IReadOnlyList<ExternalIdDto> ExternalIds { get; init; } = [];

    public static PersonRoleDto FromDomain(BasePersonRole domain) => domain switch
    {
        Actor actor => new ActorDto()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null,
            Media = LiteMediaDto.FromDomain(domain.Media),
            Person = LitePersonDto.FromDomain(domain.Person),
            ExternalIds = domain.ExternalIds.Select(ExternalIdDto.FromDomain).ToList(),
            CharacterName = actor.CharacterName
        },
        CrewMember crewMember => new CrewMemberDto()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null,
            Media = LiteMediaDto.FromDomain(domain.Media),
            Person = LitePersonDto.FromDomain(domain.Person),
            ExternalIds = domain.ExternalIds.Select(ExternalIdDto.FromDomain).ToList(),
            Department = crewMember.Department,
            Job = crewMember.Department
        },
        MusicArtist musicArtist => new MusicArtistRoleDto()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            PortraitPicture = domain.PortraitPicture != null ? MetadataPictureDto.FromDomain(domain.PortraitPicture) : null,
            Media = LiteMediaDto.FromDomain(domain.Media),
            Person = LitePersonDto.FromDomain(domain.Person),
            ExternalIds = domain.ExternalIds.Select(ExternalIdDto.FromDomain).ToList(),
            IsGuest = musicArtist.IsGuest
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
