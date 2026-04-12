using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;

public class SerieSeason() : BaseMedia(MediaType.SerieSeason)
{
    public Guid SerieId { get; set; }
    public Serie Serie { get; set; } = null!;
    public IList<SerieEpisode> Episodes { get; set; } = [];

    public int SeasonNumber { get; set; }
    public string? Overview { get; set; }



    public void ApplyMetadata(ExternalSeasonMetadata metadata)
    {
        Title = metadata.Title ?? Title;
        Overview = metadata.Overview ?? Overview;
        ReleaseDate = metadata.AirDate ?? ReleaseDate;

        if (metadata.ExternalIds?.Count > 0)
        {
            ExternalIds.Clear();
            foreach (var ex in metadata.ExternalIds) ExternalIds.Add(ex);
        }

        if (metadata.Pictures?.Count > 0)
        {
            Pictures.Clear();
            foreach (var pic in metadata.Pictures) Pictures.Add(pic);
        }
    }
}
