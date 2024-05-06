using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Common.Models.Dtos;

public record MetadataPictureDto
{
    public Guid Id { get; init; }
    public MetadataPictureType? Type { get; init; }
    public Uri? Uri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<MetadataPicture, MetadataPictureDto>()
                .ForMember(dest => dest.Uri, x => x.MapFrom(src => new Uri($"/api/metadata-pictures/{src.Id}", UriKind.Relative)));
        }
    }
}
