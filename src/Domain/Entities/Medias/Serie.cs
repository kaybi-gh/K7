using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;
public class Serie(MediaIdentification identification) : BaseMedia(MediaType.Serie, identification)
{
    public required string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }

    public virtual IEnumerable<SerieSeason> Seasons { get; set; } = [];
    public virtual IEnumerable<SerieEpisode> Episodes { get; set; } = [];
    public virtual new SerieMetadata? Metadata { get; set; }
}
