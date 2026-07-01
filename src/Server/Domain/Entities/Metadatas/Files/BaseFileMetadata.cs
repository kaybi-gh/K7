namespace K7.Server.Domain.Entities.Metadatas.Files;
public abstract class BaseFileMetadata(FileType type) : BaseAuditableEntity
{
    public FileType Type { get; protected set; } = type;
    public required string Container { get; set; }

    public Guid IndexedFileId { get; set; }
    public IndexedFile IndexedFile { get; set; } = null!;
}
