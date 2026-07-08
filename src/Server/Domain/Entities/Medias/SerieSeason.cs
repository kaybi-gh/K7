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
        if (!IsFieldLocked(nameof(Title)))
            Title = metadata.Title ?? Title;
        if (!IsFieldLocked(nameof(SortTitle)))
            SortTitle = metadata.SortTitle ?? SortTitle;
        if (!IsFieldLocked(nameof(Overview)))
            Overview = metadata.Overview ?? Overview;
        if (!IsFieldLocked(nameof(ReleaseDate)))
            ReleaseDate = metadata.AirDate ?? ReleaseDate;

        if (!IsFieldLocked(nameof(ExternalIds)) && metadata.ExternalIds?.Count > 0)
        {
            var federationIds = ExternalIds.Where(e => e.ProviderName == "federation").ToList();
            ExternalIds.Clear();
            foreach (var ex in metadata.ExternalIds) ExternalIds.Add(ex);
            foreach (var fed in federationIds) ExternalIds.Add(fed);
        }

        if (metadata.Pictures?.Count > 0)
            ApplyMetadataPictures(metadata.Pictures);
    }
}
