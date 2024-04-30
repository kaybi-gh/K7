using MediaServer.Application.Features.MetadataPictures.Queries.GetMetadataPicture;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Entities.Metadatas.Persons;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Persons.Queries.GetPerson;

public record PersonDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Name { get; set; }
    public PersonGender? Gender { get; set; } = PersonGender.NotSpecified;
    public string? Biography { get; set; }
    public DateOnly? Birthday { get; set; }
    public DateOnly? Deathday { get; set; }
    public string? BirthPlace { get; set; }

    public virtual IList<BasePersonRole> Roles { get; set; } = [];
    public virtual IList<ExternalId> ExternalIds { get; set; } = [];
    public virtual MetadataPictureDto? PortraitPicture { get; set; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<Person, PersonDto>()
                .ForMember(dst => dst.Slug, x => x.MapFrom(src => src.Slug))
                .ForMember(dst => dst.Name, x => x.MapFrom(src => src.Name))
                .ForMember(dst => dst.Gender, x => x.MapFrom(src => src.Gender))
                .ForMember(dst => dst.Biography, x => x.MapFrom(src => src.Biography))
                .ForMember(dst => dst.Birthday, x => x.MapFrom(src => src.Birthday))
                .ForMember(dst => dst.Deathday, x => x.MapFrom(src => src.Deathday))
                .ForMember(dst => dst.BirthPlace, x => x.MapFrom(src => src.BirthPlace))
                .ForMember(dst => dst.Roles, x => x.MapFrom(src => src.Roles))
                .ForMember(dst => dst.ExternalIds, x => x.MapFrom(src => src.ExternalIds))
                .ForMember(dst => dst.PortraitPicture, x => x.MapFrom(src => src.PortraitPicture));
        }
    }
}

