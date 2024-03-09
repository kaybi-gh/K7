using MediaServer.Domain.Entities.Metadatas;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Domain.Entities.Medias;

public abstract class BaseMedia(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;
    public required MediaIdentification Identification { get; set; }
    public virtual BaseMetadata? Metadata { get; set; }

    public required int LibraryId { get; set; }
    public virtual Library? Library { get; set; }
    public virtual ICollection<IndexedFile>? IndexedFiles { get; set; }
}
