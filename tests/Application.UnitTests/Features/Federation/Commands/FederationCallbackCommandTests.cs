using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Commands.ReceivePeerReject;
using K7.Server.Application.Features.Federation.Commands.ReceiveProviderRevocation;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Commands;

[TestFixture]
public class FederationCallbackCommandTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private const string Secret = "shared-assertion-secret";
    private const string ProviderUrl = "http://provider:7080";

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ReceiveProviderRevocation_ShouldRevokeRequesterPeer_WhenSignatureMatches()
    {
        var peer = PeerServer.CreatePending("Provider", ProviderUrl, "pending");
        peer.ActivateFromConfirmation("outbound-client", "outbound-secret", Secret);
        _context.PeerServers.Add(peer);
        await _context.SaveChangesAsync();

        var notifier = Substitute.For<IFederationNotifier>();
        var handler = new ReceiveProviderRevocationCommandHandler(_context, notifier);
        var proof = FederationCallbackProof.CreateProviderRevoke(ProviderUrl + "/", Secret);

        await handler.Handle(new ReceiveProviderRevocationCommand(proof), CancellationToken.None);

        var stored = await _context.PeerServers.SingleAsync();
        stored.Status.Should().Be(PeerStatus.Revoked);
    }

    [Test]
    public async Task ReceiveProviderRevocation_ShouldReject_WhenSignatureOrDirectionIsWrong()
    {
        var requester = PeerServer.CreatePending("Provider", ProviderUrl, "pending");
        requester.ActivateFromConfirmation("outbound-client", "outbound-secret", Secret);
        var inbound = PeerServer.CreateActiveInbound("Requester", "http://requester:7080", "inbound-app", false, Secret);
        _context.PeerServers.AddRange(requester, inbound);
        await _context.SaveChangesAsync();

        var handler = new ReceiveProviderRevocationCommandHandler(_context, Substitute.For<IFederationNotifier>());
        var forged = FederationCallbackProof.CreateProviderRevoke(ProviderUrl, "wrong-secret");
        var inboundProof = FederationCallbackProof.CreateProviderRevoke("http://requester:7080", Secret);

        var forgedAct = () => handler.Handle(new ReceiveProviderRevocationCommand(forged), CancellationToken.None);
        var inboundAct = () => handler.Handle(new ReceiveProviderRevocationCommand(inboundProof), CancellationToken.None);

        await forgedAct.Should().ThrowAsync<UnauthorizedAccessException>();
        await inboundAct.Should().ThrowAsync<UnauthorizedAccessException>();

        var statuses = await _context.PeerServers.Select(p => p.Status).ToListAsync();
        statuses.Should().AllBeEquivalentTo(PeerStatus.Active);
    }

    [Test]
    public async Task ReceivePeerReject_ShouldRejectPendingPeer_OnlyWhenTokenMatches()
    {
        var peer = PeerServer.CreatePending("Provider", ProviderUrl, "peering-token");
        _context.PeerServers.Add(peer);
        await _context.SaveChangesAsync();

        var handler = new ReceivePeerRejectCommandHandler(_context);
        var wrong = () => handler.Handle(
            new ReceivePeerRejectCommand(new PeerRejectRequest(ProviderUrl, "other-token")),
            CancellationToken.None);

        await wrong.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.PeerServers.SingleAsync()).Status.Should().Be(PeerStatus.Pending);

        await handler.Handle(
            new ReceivePeerRejectCommand(new PeerRejectRequest("http://not-used:7080", "peering-token")),
            CancellationToken.None);

        (await _context.PeerServers.SingleAsync()).Status.Should().Be(PeerStatus.Rejected);
    }
}
