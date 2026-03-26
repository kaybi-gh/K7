namespace K7.Server.Domain.Entities.Medias;
public class SerieSeason() : BaseMedia(MediaType.SerieSeason)
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;
    public IList<SerieEpisode> Episodes { get; set; } = [];

    public override string GetSlugSource() => $"{Serie.Slug}-{Title}";
}
