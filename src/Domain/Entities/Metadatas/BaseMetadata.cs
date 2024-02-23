using MediaServer.Domain.Entities.Medias;

namespace MediaServer.Domain.Entities.Metadatas;
public abstract class BaseMetadata : BaseAuditableEntity
{
    protected BaseMetadata(MediaType type)
    {
        Type = type;
    }

    public MediaType Type { get; protected set; }
    public required int MediaId { get; set; }

    public virtual required BaseMedia Media { get; set; }
}
