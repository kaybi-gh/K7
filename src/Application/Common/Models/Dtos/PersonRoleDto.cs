using System.Text.Json.Serialization;
using MediaServer.Application.Features.Persons.Queries.GetPerson;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Common.Models.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ActorDto), nameof(Actor))]
public abstract record PersonRoleDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public PersonJob Job { get; init; }
    public int? Order { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }
    public LiteMediaDto? Media { get; init; }
    public LitePersonDto? Person { get; init; }
    public IList<ExternalIdDto> ExternalIds { get; init; } = [];

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BasePersonRole, PersonRoleDto>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Media, x => x.MapFrom(src => src.Metadata.Media))
                .ForMember(dst => dst.Person, x => x.MapFrom(src => src.Person));

            CreateMap<Actor, ActorDto>();
        }
    }
}
