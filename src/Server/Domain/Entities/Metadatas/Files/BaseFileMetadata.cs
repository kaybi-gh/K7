namespace K7.Server.Domain.Entities.Metadatas.Files;
public abstract class BaseFileMetadata(FileType type) : BaseAuditableEntity
{
    public FileType Type { get; protected set; } = type;
    public TimeSpan Duration { get; set; }

    public Guid IndexedFileId { get; set; }
    public virtual IndexedFile IndexedFile { get; set; } = null!;
    // TODO - Move HlsSegments to concretes since some media types like ebooks won't have any
    public ICollection<HlsSegment> HlsSegments { get; set; } = [];
}
