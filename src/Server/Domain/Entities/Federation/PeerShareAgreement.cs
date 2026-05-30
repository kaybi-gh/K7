namespace K7.Server.Domain.Entities.Federation;

public class PeerShareAgreement : BaseAuditableEntity
{
    public required Guid PeerServerId { get; set; }
    public PeerServer? PeerServer { get; set; }

    public required Guid LibraryId { get; set; }
    public Library? Library { get; set; }

    public required ShareDirection Direction { get; set; }
    public int? MaxConcurrentStreams { get; set; }
    public bool IsEnabled { get; set; } = true;
}
