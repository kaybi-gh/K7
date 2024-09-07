namespace MediaServer.Domain.Entities.Metadatas.Files;
public abstract class BaseFileMetadata(FileType type) : BaseAuditableEntity
{
    public FileType Type { get; protected set; } = type;
}
