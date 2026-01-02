using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Medias;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(LiteMovieDto), nameof(Movie))]
public abstract record LiteMediaDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public string? ReleaseDate { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }

    public static LiteMediaDto FromDomain(BaseMedia domain) => domain switch
    {
        Movie movie => new LiteMovieDto()
        {
            Id = domain.Id,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate?.ToString(),
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain)
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
