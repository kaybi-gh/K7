using MediaServer.Domain.Entities.Metadatas.Files.Tracks;

namespace MediaServer.Domain.Entities.Metadatas.Files;
public abstract class BaseFileMetadata(FileType type) : BaseAuditableEntity
{
    public FileType Type { get; protected set; } = type;
    public TimeSpan Duration { get; set; }

    public Guid IndexedFileId { get; set; }
    public virtual IndexedFile IndexedFile { get; set; } = null!;
    public IEnumerable<HlsSegment> HlsSegments { get; set; } = [];
}
