using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;
public class SerieEpisode(MediaIdentification identification) : BaseMedia(MediaType.Serie, identification)
{
    public int Number { get; set; }
    public string? Title { get; set; }
    public int? SerieId { get; set; }
    public int? SeasonId { get; set; }

    public virtual Serie? Serie { get; set; }
    public virtual SerieSeason? Season { get; set; }
    public virtual new SerieEpisodeMetadata? Metadata { get; set; }
}
