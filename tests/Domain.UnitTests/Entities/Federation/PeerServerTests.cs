using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.UnitTests.Entities.Federation;

[TestFixture]
public class PeerServerTests
{
    [Test]
    public void CreatePending_ShouldStartPendingWithPeeringToken()
    {
        var peer = PeerServer.CreatePending("Peer", "https://peer.example", "token");

        peer.Status.Should().Be(PeerStatus.Pending);
        peer.Name.Should().Be("Peer");
        peer.BaseUrl.Should().Be("https://peer.example");
        peer.PeeringToken.Should().Be("token");
    }

    [Test]
    public void ActivateFromConfirmation_ShouldClearTokenAndSetActive()
    {
        var peer = PeerServer.CreatePending("Peer", "https://peer.example", "token");

        peer.ActivateFromConfirmation("client", "secret", "assertion");

        peer.Status.Should().Be(PeerStatus.Active);
        peer.PeeringToken.Should().BeNull();
        peer.OutboundClientId.Should().Be("client");
        peer.OutboundClientSecret.Should().Be("secret");
        peer.FederationAssertionSecret.Should().Be("assertion");
        peer.LastSeen.Should().NotBeNull();
    }

    [Test]
    public void Revoke_ShouldSetRevokedStatus()
    {
        var peer = PeerServer.CreateActiveInbound("Peer", "https://peer.example", "app", true, "secret");

        peer.Revoke();

        peer.Status.Should().Be(PeerStatus.Revoked);
    }

    [Test]
    public void Reject_ShouldSetRejectedStatus()
    {
        var peer = PeerServer.CreatePending("Peer", "https://peer.example", "token");

        peer.Reject();

        peer.Status.Should().Be(PeerStatus.Rejected);
    }

    [Test]
    public void MarkSeen_ShouldUpdateLastSeenAndOptionalTestResult()
    {
        var peer = PeerServer.CreatePending("Peer", "https://peer.example", "token");

        peer.MarkSeen(true);

        peer.LastSeen.Should().NotBeNull();
        peer.LastTestSucceeded.Should().BeTrue();
    }
}
