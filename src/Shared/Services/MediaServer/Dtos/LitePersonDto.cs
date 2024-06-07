using AutoMapper;
using MediaClient.Shared.Domain.Enums;
using MediaClient.Shared.Domain.Models;
using MediaClient.Shared.Services.MediaServer.Mappings;
using System.Text.Json.Serialization;

namespace MediaClient.Shared.Services.MediaServer.Dtos;

public record LitePersonDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<LitePersonDto, LitePerson>()
                .ForMember(dst => dst.Id, x => x.MapFrom(src => src.Id))
                .ForMember(dst => dst.Slug, x => x.MapFrom(src => src.Slug))
                .ForMember(dst => dst.Name, x => x.MapFrom(src => src.Name))
                .ForMember(dst => dst.Gender, x => x.MapFrom(src => src.Gender))
                .ForMember(dst => dst.Birthday, x => x.MapFrom(src => src.Birthday))
                .ForMember(dst => dst.Deathday, x => x.MapFrom(src => src.Deathday))
                .ForMember(dst => dst.BirthPlace, x => x.MapFrom(src => src.BirthPlace))
                .ForMember(dst => dst.PortraitPictureHref, x =>
                {
                    x.PreCondition(src => src.PortraitPicture != null);
                    x.MapFrom<MediaServerBaseUrlPathResolver, MetadataPictureDto>(src => src.PortraitPicture!);
                });
        }
    }
}

