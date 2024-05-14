using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Common.Models.Dtos;

public record PersonDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Name { get; init; }
    public PersonGender Gender { get; init; } = PersonGender.NotSpecified;
    public string? Biography { get; init; }
    public DateOnly? Birthday { get; init; }
    public DateOnly? Deathday { get; init; }
    public string? BirthPlace { get; init; }

    public virtual IList<PersonRoleDto> Roles { get; init; } = [];
    public virtual IList<ExternalIdDto> ExternalIds { get; init; } = [];
    public virtual MetadataPictureDto? PortraitPicture { get; init; }

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

