using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Federation;

public class PeerServer : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public PeerStatus Status { get; private set; } = PeerStatus.Pending;

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

    public void ActivateFromConfirmation(string clientId, string clientSecret, string? federationAssertionSecret)
    {
        PeeringToken = null;
        OutboundClientId = clientId;
        OutboundClientSecret = clientSecret;
        FederationAssertionSecret = federationAssertionSecret;
        Status = PeerStatus.Active;
        LastSeen = DateTimeOffset.UtcNow;
    }

    public void Revoke() => Status = PeerStatus.Revoked;

    public void Reject() => Status = PeerStatus.Rejected;

    public void MarkSeen(bool? testSucceeded = null)
    {
        LastSeen = DateTimeOffset.UtcNow;
        if (testSucceeded.HasValue)
            LastTestSucceeded = testSucceeded.Value;
    }

    public static PeerServer CreatePending(string name, string baseUrl, string peeringToken)
    {
        return new PeerServer
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseUrl = baseUrl,
            PeeringToken = peeringToken
        };
    }

    public static PeerServer CreateActiveInbound(
        string name,
        string baseUrl,
        string inboundApplicationId,
        bool autoAddNewLibraries,
        string federationAssertionSecret)
    {
        var peer = new PeerServer
        {
            Id = Guid.NewGuid(),
            Name = name,
            BaseUrl = baseUrl,
            InboundApplicationId = inboundApplicationId,
            AutoAddNewLibraries = autoAddNewLibraries,
            FederationAssertionSecret = federationAssertionSecret
        };
        peer.Status = PeerStatus.Active;
        return peer;
    }
}
