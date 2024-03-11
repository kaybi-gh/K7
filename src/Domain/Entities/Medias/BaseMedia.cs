using MediaServer.Domain.Entities.Metadatas;

namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;
    public virtual BaseMetadata? Metadata { get; set; }

    public virtual IList<IndexedFile>? IndexedFiles { get; set; }
}
