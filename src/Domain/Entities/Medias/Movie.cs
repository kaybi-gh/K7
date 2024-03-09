using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;
public class Movie : BaseMedia
{
    public Movie() : base(MediaType.Movie) { }

    public virtual MovieMetadata? Metadata { get; set; }
}
