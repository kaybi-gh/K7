using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class Movie() : BaseMedia(MediaType.Movie)
{
    public virtual new MovieMetadata? Metadata { get; set; }
}
