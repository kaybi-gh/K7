using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;
public class MusicAlbum() : BaseMedia(MediaType.MusicAlbum)
{
    public string? Overview { get; set; }

    public Guid? ArtistId { get; set; }
    public MusicArtist? Artist { get; set; }

    public IList<MusicTrack> Tracks { get; set; } = [];
    public IList<MusicArtistCredit> ArtistCredits { get; set; } = [];


    public void ApplyMetadata(ExternalMusicAlbumMetadata metadata)
    {
        if (!IsFieldLocked(nameof(Title)))
            Title = metadata.Title ?? Title;
        if (!IsFieldLocked(nameof(ReleaseDate)))
            ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        if (!IsFieldLocked(nameof(Overview)))
            Overview = metadata.Overview ?? Overview;

        if (!IsFieldLocked(nameof(Genres)) && metadata.Genres is { Count: > 0 })
        {
            Genres.Clear();
            foreach (var genre in metadata.Genres) Genres.Add(genre);
        }

        if (!IsFieldLocked(nameof(ExternalIds)) && metadata.ExternalIds is { Count: > 0 })
        {
            foreach (var externalId in metadata.ExternalIds)
            {
                if (!ExternalIds.Any(e => e.ProviderName == externalId.ProviderName && e.Value == externalId.Value))
                {
                    ExternalIds.Add(externalId);
                }
            }
        }

        if (!IsFieldLocked(nameof(Pictures)) && metadata.Pictures is { Count: > 0 })
        {
            var hasLocalCover = Pictures.Any(p => p.Type == MetadataPictureType.Cover && p.LocalPath is not null);
            foreach (var pic in metadata.Pictures)
            {
                if (hasLocalCover && pic.Type == MetadataPictureType.Cover)
                    continue;
                if (pic.OriginalRemoteUri != null && !Pictures.Any(p => p.OriginalRemoteUri == pic.OriginalRemoteUri))
                    Pictures.Add(pic);
            }
        }
    }
}
