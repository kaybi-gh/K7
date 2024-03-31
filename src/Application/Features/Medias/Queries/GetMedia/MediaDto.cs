using System.Text.Json.Serialization;
using MediaServer.Application.Features.IndexedFiles.Queries.GetIndexedFiles;
using MediaServer.Application.Features.MediaPictures.Queries.GetMediaPicture;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Application.Features.Medias.Queries.GetMedia;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MovieDto), nameof(Movie))]
public abstract record MediaDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IEnumerable<PictureDto>? Pictures { get; init; }
    public IEnumerable<RatingDto>? Ratings { get; init; }
    public IEnumerable<IndexedFileDto>? IndexedFiles { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, MediaDto>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Pictures, x => x.MapFrom(src => src.Metadata!.Pictures))
                .ForMember(dst => dst.Title, x => x.MapFrom(src => src.Metadata!.Title))
                .ForMember(dst => dst.ReleaseDate, x => x.MapFrom(src => src.Metadata!.ReleaseDate))
                .ForMember(dst => dst.IndexedFiles, x => x.MapFrom(src => src.IndexedFiles));

            CreateMap<Movie, MovieDto>()
                .ForMember(dst => dst.TagLine, x => x.MapFrom(src => (src.Metadata as MovieMetadata)!.TagLine))
                .ForMember(dst => dst.Overview, x => x.MapFrom(src => (src.Metadata as MovieMetadata)!.Overview))
                .ForMember(dst => dst.OriginalLanguage, x => x.MapFrom(src => (src.Metadata as MovieMetadata)!.OriginalLanguage));
        }
    }
}

public record MovieDto : MediaDto
{
    public string? TagLine { get; init; }
    public string? Overview { get; internal set; }
    public string? OriginalLanguage { get; internal set; }
}

