namespace K7.Server.Domain.Entities.Federation;

public class PeerServer : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public PeerStatus Status { get; set; } = PeerStatus.Pending;

    public string? OutboundClientId { get; set; }
    public string? OutboundClientSecret { get; set; }
    public string? InboundApplicationId { get; set; }

    public bool AutoAddNewLibraries { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
    public bool? LastTestSucceeded { get; set; }
    public string? FederationAssertionSecret { get; set; }
    public string? PeeringToken { get; set; }

    public IList<PeerShareAgreement> ShareAgreements { get; set; } = [];
    public IList<PeerSocialAgreement> SocialAgreements { get; set; } = [];
    public IList<Library> RemoteLibraries { get; set; } = [];
    public IList<RemoteIndexedFile> RemoteIndexedFiles { get; set; } = [];
}
