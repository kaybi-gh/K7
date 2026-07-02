using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;
public class Movie() : BaseMedia(MediaType.Movie)
{
    public string? Tagline { get; set; }
    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }
    public long? Budget { get; set; }
    public long? Revenue { get; set; }

    public void ApplyMetadata(ExternalMovieMetadata metadata)
    {
        if (!IsFieldLocked(nameof(Title)))
            Title = metadata.Title ?? Title;
        if (!IsFieldLocked(nameof(SortTitle)))
            SortTitle = metadata.SortTitle ?? SortTitle;
        if (!IsFieldLocked(nameof(OriginalTitle)))
            OriginalTitle = metadata.OriginalTitle ?? OriginalTitle;
        if (!IsFieldLocked(nameof(ReleaseDate)))
            ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        if (!IsFieldLocked(nameof(Overview)))
            Overview = metadata.Overview ?? Overview;
        if (!IsFieldLocked(nameof(Tagline)))
            Tagline = metadata.Tagline ?? Tagline;
        if (!IsFieldLocked(nameof(OriginalLanguage)))
            OriginalLanguage = metadata.OriginalLanguage ?? OriginalLanguage;
        if (!IsFieldLocked(nameof(Budget)))
            Budget = metadata.Budget ?? Budget;
        if (!IsFieldLocked(nameof(Revenue)))
            Revenue = metadata.Revenue ?? Revenue;

        if (!IsFieldLocked(nameof(Trailers)) && metadata.Trailers?.Count > 0)
        {
            Trailers.Clear();
            foreach (var trailer in metadata.Trailers) Trailers.Add(trailer);
        }

        if (!IsFieldLocked(nameof(PersonRoles)) && metadata.PersonRoles?.Count > 0)
        {
            PersonRoles.Clear();
            foreach (var role in metadata.PersonRoles) PersonRoles.Add(role);
        }

        if (!IsFieldLocked(nameof(ExternalIds)) && metadata.ExternalIds?.Count > 0)
        {
            var federationIds = ExternalIds.Where(e => e.ProviderName == "federation").ToList();
            ExternalIds.Clear();
            foreach (var ex in metadata.ExternalIds) ExternalIds.Add(ex);
            foreach (var fed in federationIds) ExternalIds.Add(fed);
        }

        if (!IsFieldLocked(nameof(Pictures)) && metadata.Pictures?.Count > 0)
        {
            Pictures.Clear();
            foreach (var pic in metadata.Pictures) Pictures.Add(pic);
        }
    }
}
