using System.Text.Json.Serialization;
using MediaServer.Application.Features.MetadataPictures.Queries.GetMetadataPicture;
using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LiteMovieDto), nameof(Movie))]
public abstract record LiteMediaDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, LiteMediaDto>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Pictures, x => x.MapFrom(src => src.Metadata!.Pictures))
                .ForMember(dst => dst.Title, x => x.MapFrom(src => src.Metadata!.Title))
                .ForMember(dst => dst.ReleaseDate, x => x.MapFrom(src => src.Metadata!.ReleaseDate));

            CreateMap<Movie, LiteMovieDto>();
        }
    }
}

public record LiteMovieDto : LiteMediaDto
{
}
