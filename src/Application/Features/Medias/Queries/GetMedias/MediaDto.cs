using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

public record MediaDto
{
    public int Id { get; init; }
    public MediaType? Type { get; init; }
    public string? Title { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, MediaDto>()
                .ForMember(dest => dest.Title, o => o.MapFrom(src => ((MovieMetadata)src.Metadata!).Title));
        }
    }
}
