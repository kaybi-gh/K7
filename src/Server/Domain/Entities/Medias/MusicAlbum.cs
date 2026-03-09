using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;
public class MusicAlbum() : BaseMedia(MediaType.MusicAlbum)
{
    public string? Overview { get; set; }

    public virtual IList<MusicTrack> Tracks { get; set; } = [];

    public override string GetSlugSource() => $"{Title}-{ReleaseDate?.Year}";

    public void ApplyMetadata(ExternalMusicAlbumMetadata metadata)
    {
        Title = metadata.Title ?? Title;
        ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        Overview = metadata.Overview ?? Overview;

        if (metadata.Genres is { Count: > 0 })
        {
            Genres.Clear();
            foreach (var genre in metadata.Genres) Genres.Add(genre);
        }

        if (metadata.ExternalIds is { Count: > 0 })
        {
            foreach (var externalId in metadata.ExternalIds)
            {
                if (!ExternalIds.Any(e => e.Platform == externalId.Platform && e.Value == externalId.Value))
                {
                    ExternalIds.Add(externalId);
                }
            }
        }

        if (metadata.Pictures is { Count: > 0 } && Pictures.Count == 0)
        {
            foreach (var pic in metadata.Pictures) Pictures.Add(pic);
        }
    }
}
