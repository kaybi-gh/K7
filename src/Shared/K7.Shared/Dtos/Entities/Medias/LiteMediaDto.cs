using System.Text.Json.Serialization;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Medias;

[JsonDerivedType(typeof(LiteMovieDto), nameof(Movie))]
[JsonDerivedType(typeof(LiteMusicAlbumDto), nameof(MusicAlbum))]
[JsonDerivedType(typeof(LiteMusicTrackDto), nameof(MusicTrack))]
public abstract record LiteMediaDto
{
    public Guid Id { get; init; }
    public string? Title { get; init; }
    public string? ReleaseDate { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }
    public UserMediaStateDto? UserState { get; init; }
    public int? UserRating { get; init; }

    private static int? GetUserRating(BaseMedia domain) =>
        domain.Ratings.OfType<UserRating>().FirstOrDefault()?.Value is double v ? (int)v : null;

    public static LiteMediaDto FromDomain(BaseMedia domain) => domain switch
    {
        Movie movie => new LiteMovieDto()
        {
            Id = domain.Id,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate?.ToString(),
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain),
            UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                ? UserMediaStateDto.FromDomain(state)
                : null,
            UserRating = GetUserRating(domain)
        },
        MusicAlbum album => new LiteMusicAlbumDto()
        {
            Id = domain.Id,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate?.ToString(),
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain),
            UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                ? UserMediaStateDto.FromDomain(state)
                : null,
            UserRating = GetUserRating(domain)
        },
        MusicTrack track => new LiteMusicTrackDto()
        {
            Id = domain.Id,
            Title = domain.Title,
            ReleaseDate = domain.ReleaseDate?.ToString(),
            Pictures = domain.Pictures.Select(MetadataPictureDto.FromDomain),
            AlbumId = track.AlbumId,
            TrackNumber = track.TrackNumber,
            IndexedFileId = domain.IndexedFiles.FirstOrDefault()?.Id,
            Duration = (domain.IndexedFiles.FirstOrDefault()?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds,
            AlbumTitle = track.Album?.Title,
            ArtistName = track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.Person?.Name,
            ArtistPersonId = track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.PersonId,
            Genre = track.Album?.Genres.FirstOrDefault() ?? domain.Genres.FirstOrDefault(),
            Bpm = track.AudioAnalysis?.Bpm,
            MusicalKey = track.AudioAnalysis?.MusicalKey,
            Energy = track.AudioAnalysis?.Energy,
            UserState = domain.UserMediaStates.FirstOrDefault() is { } state
                ? UserMediaStateDto.FromDomain(state)
                : null,
            UserRating = GetUserRating(domain)
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
