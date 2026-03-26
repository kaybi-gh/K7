using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record PlaylistItemDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int Order { get; init; }
    public string? MediaTitle { get; init; }
    public string? ArtistName { get; init; }
    public Guid? ArtistPersonId { get; init; }
    public string? AlbumTitle { get; init; }
    public string? Genre { get; init; }
    public Guid? IndexedFileId { get; init; }
    public double? Duration { get; init; }
    public int? UserRating { get; init; }
    public double? Bpm { get; init; }
    public string? MusicalKey { get; init; }
    public double? Energy { get; init; }
    public IReadOnlyList<MetadataPictureDto>? Pictures { get; init; }

    public static PlaylistItemDto FromDomain(PlaylistItem domain)
    {
        var media = domain.Media;
        var indexedFile = media?.IndexedFiles.FirstOrDefault();

        var artistRole = media is MusicTrack track
            ? track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()
            : media?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault();

        var genre = media is MusicTrack t2
            ? (t2.Album?.Genres?.FirstOrDefault() ?? t2.Genres?.FirstOrDefault())
            : media?.Genres?.FirstOrDefault();

        return new()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            MediaTitle = media?.Title,
            ArtistName = artistRole?.Person?.Name,
            ArtistPersonId = artistRole?.PersonId,
            AlbumTitle = media is MusicTrack t ? t.Album?.Title : null,
            Genre = genre,
            IndexedFileId = indexedFile?.Id,
            Duration = (indexedFile?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds,
            UserRating = media?.Ratings.OfType<UserRating>().FirstOrDefault()?.Value is double v ? (int)v : null,
            Bpm = media is MusicTrack mt ? mt.AudioAnalysis?.Bpm : null,
            MusicalKey = media is MusicTrack mk ? mk.AudioAnalysis?.MusicalKey : null,
            Energy = media is MusicTrack me ? me.AudioAnalysis?.Energy : null,
            Pictures = media?.Pictures.Select(MetadataPictureDto.FromDomain).ToList()
        };
    }
}
