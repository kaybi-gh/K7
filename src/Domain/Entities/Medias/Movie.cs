using MediaServer.Domain.Entities.Metadatas.Medias;

namespace MediaServer.Domain.Entities.Medias;
public class Movie() : BaseMedia(MediaType.Movie)
{
    public override string GetSlugSource() => $"{((MovieMetadata?)Metadata)?.Title}-{((MovieMetadata?)Metadata)?.ReleaseDate?.Year}";
}
