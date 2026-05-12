using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Medias;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(LiteMovieDto), nameof(Movie))]
[JsonDerivedType(typeof(LiteMusicAlbumDto), nameof(MusicAlbum))]
[JsonDerivedType(typeof(LiteMusicTrackDto), nameof(MusicTrack))]
[JsonDerivedType(typeof(LiteSerieDto), nameof(Serie))]
[JsonDerivedType(typeof(LiteSerieEpisodeDto), nameof(SerieEpisode))]
[JsonDerivedType(typeof(LiteSerieSeasonDto), nameof(SerieSeason))]
public abstract record LiteMediaDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public DateTimeOffset? Created { get; init; }
    public IReadOnlyList<MetadataPictureDto>? Pictures { get; init; }
    public UserMediaStateDto? UserState { get; init; }
    public int? UserRating { get; init; }
}
