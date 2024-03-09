using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;
public class Movie(MediaIdentification identification) : BaseMedia(MediaType.Movie, identification)
{
    public virtual new MovieMetadata? Metadata { get; set; }
}
