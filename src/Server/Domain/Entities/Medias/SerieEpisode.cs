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

    /// <summary>
    /// Version of the intro/outro detection that last analysed this episode to the end, whether
    /// or not it found segments. Null until then. An episode analysed by the current version and
    /// still without an intro or outro has none to find, and is no longer reported as missing.
    /// </summary>
    public int? SegmentDetectionVersion { get; set; }

    public void ApplyMetadata(ExternalEpisodeMetadata metadata)
    {
        if (!IsFieldLocked(nameof(Title)))
            Title = metadata.Title ?? Title;
        if (!IsFieldLocked(nameof(SortTitle)))
            SortTitle = metadata.SortTitle ?? SortTitle;
        if (!IsFieldLocked(nameof(Overview)))
            Overview = metadata.Overview ?? Overview;
        if (!IsFieldLocked(nameof(AirDate)))
            AirDate = metadata.AirDate ?? AirDate;
        if (!IsFieldLocked(nameof(Runtime)))
            Runtime = metadata.Runtime ?? Runtime;

        if (!IsFieldLocked(nameof(ExternalIds)) && metadata.ExternalIds?.Count > 0)
        {
            var federationIds = ExternalIds.Where(e => e.ProviderName == "federation").ToList();
            ExternalIds.Clear();
            foreach (var ex in metadata.ExternalIds) ExternalIds.Add(ex);
            foreach (var fed in federationIds) ExternalIds.Add(fed);
        }
    }
}
