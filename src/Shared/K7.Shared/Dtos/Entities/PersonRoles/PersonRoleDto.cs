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

}
