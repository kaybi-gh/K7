namespace MediaServer.Domain.Entities.Medias.Metadatas;
public abstract class BaseMetadata : BaseAuditableEntity
{
    public int MediaItemId { get; set; }

    public virtual required BaseMedia MediaItem { get; set; }
}
