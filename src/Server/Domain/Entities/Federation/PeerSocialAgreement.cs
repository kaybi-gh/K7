using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Federation;

public class PeerSocialAgreement : BaseAuditableEntity
{
    public required Guid PeerServerId { get; set; }
    public PeerServer? PeerServer { get; set; }

    public required FederationContentType ContentType { get; set; }
    public bool AllowOutbound { get; set; } = true;
    public bool AllowInbound { get; set; } = true;
}
