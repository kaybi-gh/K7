using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities.Metadatas;
public abstract class BaseMetadata(MediaType type) : BaseAuditableEntity
{
    public MediaType Type { get; protected set; } = type;
    public required int MediaId { get; set; }

    public virtual required BaseMedia Media { get; set; }
}
