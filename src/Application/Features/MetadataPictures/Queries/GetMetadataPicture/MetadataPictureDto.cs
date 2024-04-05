using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.MetadataPictures.Queries.GetMetadataPicture;

public record MetadataPictureDto
{
    public int Id { get; init; }
    public MetadataPictureType? Type { get; init; }
    public Uri? Uri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<MetadataPicture, MetadataPictureDto>()
                .ForMember(dest => dest.Uri, x => x.MapFrom(src => new Uri($"/api/pictures/{src.Id}", UriKind.Relative)));
        }
    }
}
