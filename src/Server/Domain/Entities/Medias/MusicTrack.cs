using K7.Server.Domain.Entities.Metadatas.Medias;

namespace K7.Server.Domain.Entities.Medias;
public class MusicTrack() : BaseMedia(MediaType.MusicTrack)
{
    public Guid AlbumId { get; set; }
    public virtual MusicAlbum Album { get; set; } = null!;

    public override string GetSlugSource() => $"{Album.Slug}-{((MusicTrackMetadata?)Metadata)?.Title}";
}
