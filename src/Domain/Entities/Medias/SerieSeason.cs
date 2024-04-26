using MediaServer.Domain.Entities.Metadatas.Medias;

namespace MediaServer.Domain.Entities.Medias;
public class SerieSeason() : BaseMedia(MediaType.SerieSeason)
{
    public Guid SerieId { get; set; }
    public virtual Serie Serie { get; set; } = null!;
    public virtual IList<SerieEpisode> Episodes { get; set; } = [];

    public override string GetSlugSource() => $"{Serie.Slug}-{((SerieSeasonMetadata?)Metadata)?.Title}";
}
