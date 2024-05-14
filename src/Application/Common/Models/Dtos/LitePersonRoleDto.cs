using System.Text.Json.Serialization;
using MediaServer.Application.Features.Persons.Queries.GetPerson;
using MediaServer.Domain.Entities.Metadatas.Persons;

namespace MediaServer.Application.Common.Models.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LiteActorDto), nameof(Actor))]
[JsonDerivedType(typeof(LiteCrewMemberDto), nameof(CrewMember))]
public abstract record LitePersonRoleDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int? Order { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }
    public LitePersonDto? Person { get; init; }
    public IList<ExternalIdDto> ExternalIds { get; init; } = [];

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BasePersonRole, LitePersonRoleDto>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Person, x => x.MapFrom(src => src.Person));

            CreateMap<Actor, LiteActorDto>();
            CreateMap<CrewMember, LiteCrewMemberDto>();
        }
    }
}
