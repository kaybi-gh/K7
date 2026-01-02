using K7.Server.Domain.Entities.Metadatas.External;

namespace K7.Server.Domain.Entities.Medias;
public class Movie() : BaseMedia(MediaType.Movie)
{
    public string? Tagline { get; set; }
    public string? Overview { get; set; }
    public string? OriginalLanguage { get; set; }

    public override string GetSlugSource() => $"{Title}-{ReleaseDate?.Year}";

    public void ApplyMetadata(ExternalMovieMetadata metadata)
    {
        Title = metadata.Title ?? Title;
        OriginalTitle = metadata.OriginalTitle ?? OriginalTitle;
        ReleaseDate = metadata.ReleaseDate ?? ReleaseDate;
        Overview = metadata.Overview ?? Overview;
        Tagline = metadata.Tagline ?? Tagline;
        OriginalLanguage = metadata.OriginalLanguage ?? OriginalLanguage;
        Genres = metadata.Genres ?? Genres;
        PersonRoles = metadata.PersonRoles ?? PersonRoles;
        ExternalIds = metadata.ExternalIds ?? ExternalIds;
        Pictures = metadata.Pictures ?? Pictures;
    }
}
