using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;
public class Movie() : BaseMedia(MediaType.Movie)
{
    public string? Tagline { get; set; }
    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }
    public string? ContentRating { get; set; }
    public long? Budget { get; set; }
    public long? Revenue { get; set; }
    public IList<string> Studios { get; set; } = [];



    public void ApplyMetadata(ExternalMovieMetadata metadata)
    {
        Title = metadata.Title ?? Title;
        OriginalTitle = metadata.OriginalTitle ?? OriginalTitle;
        ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        Overview = metadata.Overview ?? Overview;
        Tagline = metadata.Tagline ?? Tagline;
        OriginalLanguage = metadata.OriginalLanguage ?? OriginalLanguage;
        ContentRating = metadata.ContentRating ?? ContentRating;
        Budget = metadata.Budget ?? Budget;
        Revenue = metadata.Revenue ?? Revenue;

        if (metadata.Genres?.Count > 0)
        {
            Genres.Clear();
            foreach (var genre in metadata.Genres) Genres.Add(genre);
        }

        if (metadata.Studios?.Count > 0)
        {
            Studios.Clear();
            foreach (var studio in metadata.Studios) Studios.Add(studio);
        }

        if (metadata.Trailers?.Count > 0)
        {
            Trailers.Clear();
            foreach (var trailer in metadata.Trailers) Trailers.Add(trailer);
        }

        if (metadata.PersonRoles?.Count > 0)
        {
            PersonRoles.Clear();
            foreach (var role in metadata.PersonRoles) PersonRoles.Add(role);
        }

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
