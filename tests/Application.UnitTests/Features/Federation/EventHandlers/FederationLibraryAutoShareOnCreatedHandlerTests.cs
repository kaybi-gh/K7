using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.EventHandlers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Federation.EventHandlers;

[TestFixture]
public class FederationLibraryAutoShareOnCreatedHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IPeerClient _peerClient = null!;
    private FederationLibraryAutoShareOnCreatedHandler _handler = null!;

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

        _peerClient = Substitute.For<IPeerClient>();
        _handler = new FederationLibraryAutoShareOnCreatedHandler(
            _context,
            _peerClient,
            NullLogger<FederationLibraryAutoShareOnCreatedHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateOutboundAgreements_WhenPeersHaveAutoAdd()
    {
        var autoPeer = await SeedPeerAsync("auto", autoAdd: true, withOutbound: true);
        var manualPeer = await SeedPeerAsync("manual", autoAdd: false, withOutbound: true);
        var library = await SeedLocalLibraryAsync("New Movies");

        await _handler.Handle(new LibraryCreatedEvent(library), CancellationToken.None);

        var autoAgreements = await _context.PeerShareAgreements
            .Where(a => a.PeerServerId == autoPeer.Id && a.Direction == ShareDirection.Outbound)
            .ToListAsync();
        var manualAgreements = await _context.PeerShareAgreements
            .Where(a => a.PeerServerId == manualPeer.Id && a.Direction == ShareDirection.Outbound)
            .ToListAsync();

        autoAgreements.Should().ContainSingle(a => a.LibraryId == library.Id && a.IsEnabled);
        manualAgreements.Should().BeEmpty();

        await _peerClient.Received(1).NotifyShareUpdateAsync(
            autoPeer.BaseUrl,
            "token",
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(library.Id)),
            Arg.Any<CancellationToken>());
        await _peerClient.DidNotReceive().NotifyShareUpdateAsync(
            manualPeer.BaseUrl,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldSkipNotify_WhenPeerHasNoOutboundCredentials()
    {
        var peer = await SeedPeerAsync("inbound-only", autoAdd: true, withOutbound: false);
        var library = await SeedLocalLibraryAsync("Local Only Share");

        await _handler.Handle(new LibraryCreatedEvent(library), CancellationToken.None);

        (await _context.PeerShareAgreements.CountAsync(a =>
            a.PeerServerId == peer.Id
            && a.LibraryId == library.Id
            && a.Direction == ShareDirection.Outbound)).Should().Be(1);

        await _peerClient.DidNotReceive().GetAccessTokenAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldIgnoreFederatedMirrorLibraries()
    {
        var peer = await SeedPeerAsync("auto", autoAdd: true, withOutbound: true);
        var library = await SeedLocalLibraryAsync("Remote Mirror", peer.Id);

        await _handler.Handle(new LibraryCreatedEvent(library), CancellationToken.None);

        (await _context.PeerShareAgreements.CountAsync()).Should().Be(0);
        await _peerClient.DidNotReceive().NotifyShareUpdateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldBeIdempotent_WhenAgreementAlreadyExists()
    {
        var peer = await SeedPeerAsync("auto", autoAdd: true, withOutbound: true);
        var library = await SeedLocalLibraryAsync("Movies");
        _context.PeerShareAgreements.Add(new PeerShareAgreement
        {
            Id = Guid.NewGuid(),
            PeerServerId = peer.Id,
            LibraryId = library.Id,
            Direction = ShareDirection.Outbound,
            IsEnabled = true
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new LibraryCreatedEvent(library), CancellationToken.None);

        (await _context.PeerShareAgreements.CountAsync(a =>
            a.PeerServerId == peer.Id && a.LibraryId == library.Id)).Should().Be(1);
        await _peerClient.DidNotReceive().NotifyShareUpdateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<CancellationToken>());
    }

    private async Task<PeerServer> SeedPeerAsync(string name, bool autoAdd, bool withOutbound)
    {
        var peer = PeerServer.CreateActiveInbound(
            name,
            $"https://{name}.example",
            $"app-{name}",
            autoAdd,
            "secret");

        if (withOutbound)
        {
            peer.OutboundClientId = $"out-{name}";
            peer.OutboundClientSecret = "secret-out";
            _peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId, peer.OutboundClientSecret, Arg.Any<CancellationToken>())
                .Returns("token");
        }

        _context.PeerServers.Add(peer);
        await _context.SaveChangesAsync();
        return peer;
    }

    private async Task<Library> SeedLocalLibraryAsync(string title, Guid? peerServerId = null)
    {
        var groupId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = title,
            MediaType = LibraryMediaType.Movie
        });

        var library = new Library
        {
            Id = Guid.NewGuid(),
            Title = title,
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = peerServerId is null ? "tmdb" : "federation",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = groupId,
            PeerServerId = peerServerId
        };
        _context.Libraries.Add(library);
        await _context.SaveChangesAsync();
        return library;
    }
}
