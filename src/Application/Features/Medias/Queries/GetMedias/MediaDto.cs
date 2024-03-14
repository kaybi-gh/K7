using System.Text.Json.Serialization;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

[JsonDerivedType(typeof(MovieDto))]
public abstract record MediaDto
{
    public int Id { get; init; }
    public MediaType? Type { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, MediaDto>()
                .IncludeAllDerived();

            CreateMap<Movie, MovieDto>()
                .ForMember(dst => dst.Title, src => src.MapFrom(x => (x.Metadata as MovieMetadata)!.Title));
        }
    }
}

public record MovieDto : MediaDto
{
    public string? Title { get; init; }
}
