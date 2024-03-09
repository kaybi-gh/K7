using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class SerieSeason() : BaseMedia(MediaType.SerieSeason)
{
    public int Number { get; set; }
    public int SerieId { get; set; }

    public virtual Serie? Serie { get; set; }
    public virtual IEnumerable<SerieEpisode>? Episodes { get; set; }
    public virtual new SerieSeasonMetadata? Metadata { get; set; }
}
