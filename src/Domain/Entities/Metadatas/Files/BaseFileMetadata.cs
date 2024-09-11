namespace MediaServer.Domain.Entities.Metadatas.Files;
public abstract class BaseFileMetadata(FileType type) : BaseAuditableEntity
{
    public FileType Type { get; protected set; } = type;

    public Guid IndexedFileId { get; set; }
    public virtual IndexedFile IndexedFile { get; set; } = null!;
}
