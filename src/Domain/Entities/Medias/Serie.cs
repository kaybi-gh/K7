using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class Serie : BaseMedia
{
    public Serie() : base(MediaType.Serie) { }

    public required string Title { get; set; }
    public DateOnly? ReleaseYear { get; set; }

    public virtual IEnumerable<SerieSeason> Seasons { get; set; } = [];
    public virtual IEnumerable<SerieEpisode> Episodes { get; set; } = [];
    public virtual SerieMetadata? Metadata { get; set; }
}
