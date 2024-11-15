using AutoMapper;
using MediaClient.Shared.Domain.Models;
using MediaClient.Shared.Services.MediaServer.Mappings;
using System.Text.Json.Serialization;

namespace MediaClient.Shared.Services.MediaServer.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LiteActorDto), "Actor")]
[JsonDerivedType(typeof(LiteCrewMemberDto), "CrewMember")]
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
            CreateMap<LitePersonRoleDto, LitePersonRole>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Person, x => x.MapFrom(src => src.Person))
                .ForMember(dst => dst.PortraitPictureHref, x =>
                {
                    x.PreCondition(src => src.PortraitPicture != null);
                    x.MapFrom<MediaServerAbsoluteUriResolver, Uri?>(src => src.PortraitPicture!.Uri);
                });

            CreateMap<LiteActorDto, LiteActor>();
            CreateMap<LiteCrewMemberDto, LiteCrewMember>();
        }
    }
}
