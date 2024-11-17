namespace K7.Server.Domain.Entities.Metadatas.Files.Tracks;

public abstract class BaseFileTrack(FileTrackType type) : BaseAuditableEntity
{
    public FileTrackType Type { get; protected set; } = type;
    public int Index { get; set; }
    public bool IsDefault { get; set; }
}
