using K7.Server.Domain.Entities.Metadatas.Medias;

namespace K7.Server.Domain.Entities.Medias;
public class Movie() : BaseMedia(MediaType.Movie)
{
    public override string GetSlugSource() => $"{((MovieMetadata?)Metadata)?.Title}-{((MovieMetadata?)Metadata)?.ReleaseDate?.Year}";
}
