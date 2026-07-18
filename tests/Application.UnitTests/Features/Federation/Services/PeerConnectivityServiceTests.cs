using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Events;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Services;

[TestFixture]
public class PeerConnectivityServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IFederationNotifier _federationNotifier = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private PeerConnectivityService _service = null!;

    private Guid _peerId;

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

        _federationNotifier = Substitute.For<IFederationNotifier>();
        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _service = new PeerConnectivityService(_context, _federationNotifier, _cacheInvalidator);

        var peer = PeerServer.CreateActiveInbound("Peer", "https://peer.example", "peer-app", true, "secret");
        _peerId = peer.Id;
        _context.PeerServers.Add(peer);
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task RecordConnectivityAsync_ShouldRaiseEvent_WhenTransitioningToFailed()
    {
        await _service.RecordConnectivityAsync(_peerId, succeeded: false);

        var peer = await _context.PeerServers.SingleAsync(p => p.Id == _peerId);
        peer.DomainEvents.Should().ContainSingle();
        var evt = peer.DomainEvents.OfType<PeerConnectivityChangedEvent>().Single();
        evt.Succeeded.Should().BeFalse();
        evt.PreviousSucceeded.Should().BeNull();
        evt.Peer.Id.Should().Be(_peerId);
    }

    [Test]
    public async Task RecordConnectivityAsync_ShouldRaiseEvent_WhenTransitioningFromFailedToSucceeded()
    {
        await _service.RecordConnectivityAsync(_peerId, succeeded: false);
        (await _context.PeerServers.SingleAsync(p => p.Id == _peerId)).ClearDomainEvents();

        await _service.RecordConnectivityAsync(_peerId, succeeded: true);

        var peer = await _context.PeerServers.SingleAsync(p => p.Id == _peerId);
        var evt = peer.DomainEvents.OfType<PeerConnectivityChangedEvent>().Single();
        evt.Succeeded.Should().BeTrue();
        evt.PreviousSucceeded.Should().BeFalse();
    }

    [Test]
    public async Task RecordConnectivityAsync_ShouldNotRaiseEvent_WhenStateDoesNotChange()
    {
        await _service.RecordConnectivityAsync(_peerId, succeeded: true);
        var peer = await _context.PeerServers.SingleAsync(p => p.Id == _peerId);
        peer.ClearDomainEvents();

        await _service.RecordConnectivityAsync(_peerId, succeeded: true);

        peer = await _context.PeerServers.SingleAsync(p => p.Id == _peerId);
        peer.DomainEvents.OfType<PeerConnectivityChangedEvent>().Should().BeEmpty();
    }

    [Test]
    public async Task RecordConnectivityAsync_ShouldInvalidateCache_WhenVisibilityTransitions()
    {
        await _service.RecordConnectivityAsync(_peerId, succeeded: false);

        _cacheInvalidator.Received(1).InvalidateAll();
    }

    [Test]
    public async Task RecordConnectivityAsync_ShouldNotifyPeerTestResult()
    {
        await _service.RecordConnectivityAsync(_peerId, succeeded: true);

        await _federationNotifier.Received(1).NotifyPeerTestResultAsync(_peerId, true, Arg.Any<CancellationToken>());
    }
}
