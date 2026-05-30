using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Federation;

public class RemoteIndexedFile : BaseAuditableEntity
{
    public required Guid PeerServerId { get; set; }
    public PeerServer? PeerServer { get; set; }

    public required Guid RemoteFileId { get; set; }
    public required string Name { get; set; }
    public required string Extension { get; set; }
    public required long Size { get; set; }

    public required Guid MediaId { get; set; }
    public BaseMedia? Media { get; set; }

    public required Guid RemoteMediaId { get; set; }

    public required Guid LibraryId { get; set; }
    public Library? Library { get; set; }

    public required Guid RemoteLibraryId { get; set; }
}
