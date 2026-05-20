using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;

public class Serie() : BaseMedia(MediaType.Serie)
{
    public IList<SerieSeason> Seasons { get; set; } = [];

    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? ContentRating { get; set; }
    public string? Status { get; set; }
    public string? Network { get; set; }
    public IList<string> Studios { get; set; } = [];



    public void ApplyMetadata(ExternalSerieMetadata metadata)
    {
        if (!IsFieldLocked(nameof(Title)))
            Title = metadata.Title ?? Title;
        if (!IsFieldLocked(nameof(OriginalTitle)))
            OriginalTitle = metadata.OriginalTitle ?? OriginalTitle;
        if (!IsFieldLocked(nameof(ReleaseDate)))
            ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        if (!IsFieldLocked(nameof(Overview)))
            Overview = metadata.Overview ?? Overview;
        if (!IsFieldLocked(nameof(OriginalLanguage)))
            OriginalLanguage = metadata.OriginalLanguage ?? OriginalLanguage;
        if (!IsFieldLocked(nameof(ContentRating)))
            ContentRating = metadata.ContentRating ?? ContentRating;
        if (!IsFieldLocked(nameof(Status)))
            Status = metadata.Status ?? Status;
        if (!IsFieldLocked(nameof(Network)))
            Network = metadata.Network ?? Network;

        if (!IsFieldLocked(nameof(Genres)) && metadata.Genres?.Count > 0)
        {
            Genres.Clear();
            foreach (var genre in metadata.Genres) Genres.Add(genre);
        }

        if (!IsFieldLocked(nameof(Studios)) && metadata.Studios?.Count > 0)
        {
            Studios.Clear();
            foreach (var studio in metadata.Studios) Studios.Add(studio);
        }

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
            ExternalIds.Clear();
            foreach (var ex in metadata.ExternalIds) ExternalIds.Add(ex);
        }

        if (!IsFieldLocked(nameof(Pictures)) && metadata.Pictures?.Count > 0)
        {
            Pictures.Clear();
            foreach (var pic in metadata.Pictures) Pictures.Add(pic);
        }
    }
}
