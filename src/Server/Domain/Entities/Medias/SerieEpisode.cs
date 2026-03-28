using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;

public class SerieEpisode() : BaseMedia(MediaType.SerieEpisode)
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;
    public Guid SeasonId { get; set; }
    public SerieSeason Season { get; set; } = null!;

    public int EpisodeNumber { get; set; }
    public string? Overview { get; set; }
    public int? AbsoluteNumber { get; set; }
    public DateOnly? AirDate { get; set; }
    public int? Runtime { get; set; }

    public override string GetSlugSource() => $"{Season.Slug}-{Title}";

    public void ApplyMetadata(ExternalEpisodeMetadata metadata)
    {
        Title = metadata.Title ?? Title;
        Overview = metadata.Overview ?? Overview;
        AirDate = metadata.AirDate ?? AirDate;
        Runtime = metadata.Runtime ?? Runtime;

        if (metadata.ExternalIds?.Count > 0)
        {
            ExternalIds.Clear();
            foreach (var ex in metadata.ExternalIds) ExternalIds.Add(ex);
        }
    }
}
