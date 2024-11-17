using K7.Server.Domain.Entities.Metadatas.Medias;

namespace K7.Server.Domain.Entities.Medias;
public class Serie() : BaseMedia(MediaType.Serie)
{
    public virtual IList<SerieSeason> Seasons { get; set; } = [];
    public virtual IList<SerieEpisode> Episodes { get; set; } = [];

    public override string GetSlugSource() => $"{((SerieMetadata?)Metadata)?.Title}-{((SerieMetadata?)Metadata)?.ReleaseDate?.Year}";
}
