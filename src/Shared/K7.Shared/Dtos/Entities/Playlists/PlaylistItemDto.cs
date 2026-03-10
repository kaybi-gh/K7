using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Playlists;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record PlaylistItemDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int Order { get; init; }
    public string? MediaTitle { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumTitle { get; init; }
    public Guid? IndexedFileId { get; init; }
    public double? Duration { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }

    public static PlaylistItemDto FromDomain(PlaylistItem domain)
    {
        var media = domain.Media;
        var indexedFile = media?.IndexedFiles.FirstOrDefault();

        return new()
        {
            Id = domain.Id,
            MediaId = domain.MediaId,
            Order = domain.Order,
            MediaTitle = media?.Title,
            ArtistName = media is MusicTrack track
                ? track.Album?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.Person?.Name
                : media?.PersonRoles?.OfType<MusicArtist>().FirstOrDefault()?.Person?.Name,
            AlbumTitle = media is MusicTrack t ? t.Album?.Title : null,
            IndexedFileId = indexedFile?.Id,
            Duration = (indexedFile?.FileMetadata as AudioFileMetadata)?.Duration.TotalSeconds,
            Pictures = media?.Pictures.Select(MetadataPictureDto.FromDomain)
        };
    }
}
