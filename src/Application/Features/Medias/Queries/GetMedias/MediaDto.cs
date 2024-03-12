using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

public record MediaDto
{
    public int Id { get; init; }
    public MediaType? Type { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, Movie>()
                .ForMember(dst => dst.Metadata, src => src.MapFrom(x => x.Metadata));
            CreateMap<BaseMedia, MediaDto>()
                .IncludeAllDerived();

            CreateMap<Movie, MovieDto>()
                .ForMember(dst => dst.Title, src => src.MapFrom(x => x.Metadata!.Title));
        }
    }
}

public record MovieDto : MediaDto
{
    public string? Title { get; init; }

    
}
