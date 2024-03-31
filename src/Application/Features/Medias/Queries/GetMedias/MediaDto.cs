using System.Text.Json.Serialization;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.Features.Medias.Queries.GetMedias;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MovieDto), nameof(Movie))]
public abstract record MediaDto
{
    public int Id { get; init; }
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IEnumerable<PictureDto>? Pictures { get; init; }
    public IEnumerable<RatingDto>? Ratings { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<BaseMedia, MediaDto>()
                .IncludeAllDerived()
                .ForMember(dst => dst.Pictures, x => x.MapFrom(src => src.Metadata!.Pictures))
                .ForMember(dst => dst.Ratings, x => x.MapFrom(src => src.Metadata!.Ratings))
                .ForMember(dst => dst.Title, x => x.MapFrom(src => src.Metadata!.Title))
                .ForMember(dst => dst.ReleaseDate, x => x.MapFrom(src => src.Metadata!.ReleaseDate));
        }
    }
}

public record PictureDto
{
    public int Id { get; init; }
    public MediaPictureType? Type { get; init; }
    public Uri? Uri { get; init; }

    private class Mapping : Profile
    {
        public Mapping()
        {
            CreateMap<MediaPicture, PictureDto>()
                .ForMember(dest => dest.Uri, x => x.MapFrom(src => new Uri($"/api/pictures/{src.Id}", UriKind.Relative)));
        }
    }
}

public record RatingDto
{
    public int Id { get; init; }
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}

public record MovieDto : MediaDto
{
}
