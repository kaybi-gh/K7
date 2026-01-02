namespace K7.Server.Domain.Entities.Medias;
public class SerieEpisode() : BaseMedia(MediaType.Serie)
{
    public Guid SerieId { get; set; }
    public virtual Serie Serie { get; set; } = null!;
    public Guid SeasonId { get; set; }
    public virtual SerieSeason Season { get; set; } = null!;
    
    public override string GetSlugSource() => $"{Season.Slug}-{Title}";
}
