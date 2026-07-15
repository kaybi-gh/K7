using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Services;

[TestFixture]
public class PeerAuthorizationServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IFederationViewerAssertionService _assertionService = null!;
    private IPeerClient _peerClient = null!;
    private PeerAuthorizationService _service = null!;

    private Guid _peerId;
    private Guid _libraryId;
    private Guid _mediaId;
    private const string InboundClientId = "peer-app";

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

        _assertionService = Substitute.For<IFederationViewerAssertionService>();
        _peerClient = Substitute.For<IPeerClient>();
        _service = new PeerAuthorizationService(_context, _assertionService, _peerClient);

        _peerId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _mediaId = Guid.NewGuid();

        var groupId = Guid.NewGuid();
        var peer = PeerServer.CreateActiveInbound("Peer", "https://peer.example", InboundClientId, true, "secret");
        peer.Id = _peerId;
        _context.PeerServers.Add(peer);
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Movie,
            Title = "Movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.Add(new Movie { Id = _mediaId, Title = "Local" });
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            MediaId = _mediaId,
            LibraryId = _libraryId
        });
        _context.PeerShareAgreements.Add(new PeerShareAgreement
        {
            PeerServerId = _peerId,
            LibraryId = _libraryId,
            Direction = ShareDirection.Outbound,
            IsEnabled = true
        });
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ResolveInboundPeerAsync_ShouldReturnActivePeer_WhenClientIdMatches()
    {
        var peer = await _service.ResolveInboundPeerAsync(InboundClientId);

        peer.Should().NotBeNull();
        peer!.Id.Should().Be(_peerId);
    }

    [Test]
    public async Task RequireInboundPeerAsync_ShouldThrowForbidden_WhenUnknownClient()
    {
        var act = () => _service.RequireInboundPeerAsync("unknown");

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task ResolvePeerWithViewerAsync_ShouldReturnViewer_WhenAssertionValid()
    {
        var viewer = new FederatedUserRef { OriginUserId = Guid.NewGuid(), DisplayName = "Kay" };
        _assertionService.ValidateAssertion("assert", "secret").Returns(viewer);

        var result = await _service.ResolvePeerWithViewerAsync(InboundClientId, "assert");

        result.Should().NotBeNull();
        result!.Value.Peer.Id.Should().Be(_peerId);
        result.Value.Viewer.Should().Be(viewer);
    }

    [Test]
    public async Task ResolvePeerWithViewerAsync_ShouldThrowUnauthorized_WhenAssertionInvalid()
    {
        _assertionService.ValidateAssertion(Arg.Any<string?>(), Arg.Any<string>()).Returns((FederatedUserRef?)null);

        var act = () => _service.ResolvePeerWithViewerAsync(InboundClientId, "bad");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Test]
    public async Task IsMediaAccessibleToPeerAsync_ShouldReturnTrue_WhenOutboundShareExists()
    {
        var accessible = await _service.IsMediaAccessibleToPeerAsync(_peerId, _mediaId);

        accessible.Should().BeTrue();
    }

    [Test]
    public async Task IsMediaAccessibleToPeerAsync_ShouldReturnFalse_WhenShareDisabled()
    {
        var agreement = await _context.PeerShareAgreements.SingleAsync();
        agreement.IsEnabled = false;
        await _context.SaveChangesAsync();

        var accessible = await _service.IsMediaAccessibleToPeerAsync(_peerId, _mediaId);

        accessible.Should().BeFalse();
    }

    [Test]
    public async Task RequireLibrarySharedWithPeerAsync_ShouldThrow_WhenNotShared()
    {
        var act = () => _service.RequireLibrarySharedWithPeerAsync(_peerId, Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task GetOutboundSharedLibraryIdsAsync_ShouldReturnEnabledLibraries()
    {
        var ids = await _service.GetOutboundSharedLibraryIdsAsync(_peerId);

        ids.Should().ContainSingle().Which.Should().Be(_libraryId);
    }
}
