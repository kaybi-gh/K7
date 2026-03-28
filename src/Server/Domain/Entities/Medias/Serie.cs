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

    public override string GetSlugSource() => $"{Title}-{ReleaseDate?.Year}";

    public void ApplyMetadata(ExternalSerieMetadata metadata)
    {
        Title = metadata.Title ?? Title;
        OriginalTitle = metadata.OriginalTitle ?? OriginalTitle;
        ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        Overview = metadata.Overview ?? Overview;
        OriginalLanguage = metadata.OriginalLanguage ?? OriginalLanguage;
        ContentRating = metadata.ContentRating ?? ContentRating;
        Status = metadata.Status ?? Status;
        Network = metadata.Network ?? Network;

        if (metadata.Genres?.Count > 0)
        {
            Genres.Clear();
            foreach (var genre in metadata.Genres) Genres.Add(genre);
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
