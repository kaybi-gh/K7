using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(MovieDto), nameof(Movie))]
[JsonDerivedType(typeof(MusicAlbumDto), nameof(MusicAlbum))]
[JsonDerivedType(typeof(MusicTrackDto), nameof(MusicTrack))]
[JsonDerivedType(typeof(MusicArtistDto), nameof(MusicArtist))]
[JsonDerivedType(typeof(SerieDto), nameof(Serie))]
[JsonDerivedType(typeof(SerieSeasonDto), nameof(SerieSeason))]
[JsonDerivedType(typeof(SerieEpisodeDto), nameof(SerieEpisode))]
public abstract record MediaDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IReadOnlyList<MetadataPictureDto>? Pictures { get; init; }
    public IReadOnlyList<LitePersonRoleDto>? PersonRoles { get; init; }
    public IReadOnlyList<RatingDto>? Ratings { get; init; }
    public IReadOnlyList<IndexedFileDto>? IndexedFiles { get; init; }
    public IReadOnlyList<RemoteIndexedFileDto>? RemoteIndexedFiles { get; init; }
    public IReadOnlyList<string>? Genres { get; init; }
    public IReadOnlyList<string>? LockedFields { get; init; }
    public IReadOnlyList<ExternalIdDto>? ExternalIds { get; init; }
    public UserMediaStateDto? UserState { get; init; }
    public int TotalPlayCount { get; set; }
    public Guid? LibraryId { get; set; }
    public DateTimeOffset? LastMetadataRefreshedAt { get; init; }
}
