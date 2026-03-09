using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities.PersonRoles;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(MovieDto), nameof(Movie))]
[JsonDerivedType(typeof(MusicAlbumDto), nameof(MusicAlbum))]
[JsonDerivedType(typeof(MusicTrackDto), nameof(MusicTrack))]
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
    public UserMediaStateDto? UserState { get; init; }

    public static MediaDto FromDomain(BaseMedia domain) => domain switch
    {
        Movie movie => new MovieDto()
        {
            Id = domain.Id,
            Slug = domain.Slug,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate,
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain),
            PersonRoles = domain.PersonRoles.Select(LitePersonRoleDto.FromDomain),
            Ratings = domain.Ratings.Select(RatingDto.FromDomain),
            IndexedFiles = domain.IndexedFiles.Select(IndexedFileDto.FromDomain),
            Genres = domain.Genres,
            Overview = movie.Overview,
            OriginalLanguage = movie.OriginalLanguage,
            TagLine = movie.Tagline,
            UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                ? UserMediaStateDto.FromDomain(state)
                : null
        },
        MusicAlbum album => new MusicAlbumDto()
        {
            Id = domain.Id,
            Slug = domain.Slug,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate,
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain),
            PersonRoles = domain.PersonRoles.Select(LitePersonRoleDto.FromDomain),
            Ratings = domain.Ratings.Select(RatingDto.FromDomain),
            IndexedFiles = domain.IndexedFiles.Select(IndexedFileDto.FromDomain),
            Genres = domain.Genres,
            Overview = album.Overview,
            Tracks = album.Tracks.Select(t => (LiteMusicTrackDto)LiteMediaDto.FromDomain(t)),
            UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                ? UserMediaStateDto.FromDomain(state)
                : null
        },
        MusicTrack track => new MusicTrackDto()
        {
            Id = domain.Id,
            Slug = domain.Slug,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate,
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain),
            PersonRoles = domain.PersonRoles.Select(LitePersonRoleDto.FromDomain),
            Ratings = domain.Ratings.Select(RatingDto.FromDomain),
            IndexedFiles = domain.IndexedFiles.Select(IndexedFileDto.FromDomain),
            Genres = domain.Genres,
            AlbumId = track.AlbumId,
            TrackNumber = track.TrackNumber,
            DiscNumber = track.DiscNumber,
            Lyrics = track.Lyrics,
            LyricsLrc = track.LyricsLrc,
            Bpm = track.Bpm,
            MusicalKey = track.MusicalKey,
            LoudnessLufs = track.LoudnessLufs,
            UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                ? UserMediaStateDto.FromDomain(state)
                : null
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };

    public static BaseMedia ToDomain(MediaDto dto) => dto switch
    {
        MovieDto => new Movie(),
        MusicAlbumDto => new MusicAlbum(),
        MusicTrackDto => new MusicTrack(),
        _ => throw new NotSupportedException($"Unknown type: {dto.GetType().Name}")
    };
}
