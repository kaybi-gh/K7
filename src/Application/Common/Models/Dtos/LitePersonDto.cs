using MediaServer.Application.Common.Models.Dtos;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Persons.Queries.GetPerson;

public record LitePersonDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }
    public MetadataPictureDto? PortraitPicture { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Person, LitePersonDto>()
                .ForMember(dst => dst.Slug, x => x.MapFrom(src => src.Slug))
                .ForMember(dst => dst.Name, x => x.MapFrom(src => src.Name))
                .ForMember(dst => dst.Gender, x => x.MapFrom(src => src.Gender))
                .ForMember(dst => dst.Birthday, x => x.MapFrom(src => src.Birthday))
                .ForMember(dst => dst.Deathday, x => x.MapFrom(src => src.Deathday))
                .ForMember(dst => dst.BirthPlace, x => x.MapFrom(src => src.BirthPlace))
                .ForMember(dst => dst.PortraitPicture, x => x.MapFrom(src => src.PortraitPicture));
        }
    }
}

