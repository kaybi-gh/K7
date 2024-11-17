using System.Text.Json.Serialization;
using K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFiles;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Medias;

namespace K7.Server.Application.Common.Models.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MovieDto), nameof(Movie))]
public abstract record MediaDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }
    public IEnumerable<LitePersonRoleDto>? PersonRoles { get; init; }
    public IEnumerable<RatingDto>? Ratings { get; init; }
    public IEnumerable<IndexedFileDto>? IndexedFiles { get; init; }
    public IEnumerable<string>? Genres { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, MediaDto>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Pictures, x => x.MapFrom(src => src.Metadata!.Pictures))
                .ForMember(dst => dst.Title, x => x.MapFrom(src => src.Metadata!.Title))
                .ForMember(dst => dst.ReleaseDate, x => x.MapFrom(src => src.Metadata!.ReleaseDate))
                .ForMember(dst => dst.PersonRoles, x => x.MapFrom(src => src.Metadata!.PersonRoles))
                .ForMember(dst => dst.IndexedFiles, x => x.MapFrom(src => src.IndexedFiles))
                .ForMember(dst => dst.Genres, x => x.MapFrom(src => src.Metadata!.Genres));

            CreateMap<Movie, MovieDto>()
                .ForMember(dst => dst.TagLine, x => x.MapFrom(src => (src.Metadata as MovieMetadata)!.TagLine))
                .ForMember(dst => dst.Overview, x => x.MapFrom(src => (src.Metadata as MovieMetadata)!.Overview))
                .ForMember(dst => dst.OriginalLanguage, x => x.MapFrom(src => (src.Metadata as MovieMetadata)!.OriginalLanguage));
        }
    }
}
