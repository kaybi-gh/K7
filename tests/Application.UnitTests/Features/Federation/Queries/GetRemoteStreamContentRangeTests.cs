using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Queries.GetRemoteStreamContent;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Headers;

namespace K7.Server.Application.UnitTests.Features.Federation.Queries;

[TestFixture]
public class GetRemoteStreamContentRangeTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IPeerAuthorizationService _peerAuthorization = null!;
    private IPeerClient _peerClient = null!;
    private GetRemoteStreamContentQueryHandler _handler = null!;

    private Guid _sessionId;
    private Guid _remoteSessionId;
    private Guid _peerId;
    private PeerServer _peer = null!;

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

        _sessionId = Guid.NewGuid();
        _remoteSessionId = Guid.NewGuid();
        _peerId = Guid.NewGuid();

        _peer = PeerServer.CreatePending("Peer", "https://peer.example", "token");
        _peer.Id = _peerId;
        _peer.ActivateFromConfirmation("client", "secret", "assertion");
        _context.PeerServers.Add(_peer);

        _context.StreamSessions.Add(new StreamSession
        {
            Id = _sessionId,
            PeerServerId = _peerId,
            RemoteSessionId = _remoteSessionId,
            FederatedPlaybackExecution = FederatedPlaybackExecution.Peer,
            PlaybackSettingsJson = "{}"
        });
        _context.SaveChanges();

        _peerAuthorization = Substitute.For<IPeerAuthorizationService>();
        _peerClient = Substitute.For<IPeerClient>();
        _peerAuthorization.AuthenticateOutboundAsync(_peerId, Arg.Any<CancellationToken>())
            .Returns((_peer, "bearer-token"));

        _handler = new GetRemoteStreamContentQueryHandler(_context, _peerAuthorization, _peerClient);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldForwardRange_WhenDirectStream()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([1, 2, 3])
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        response.Content.Headers.ContentLength = 3;
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 2, 100);
        response.Headers.AcceptRanges.ParseAdd("bytes");

        _peerClient.ProxyStreamContentAsync(
                "https://peer.example",
                "bearer-token",
                _remoteSessionId,
                "direct-stream",
                Arg.Any<CancellationToken>(),
                "bytes=0-2")
            .Returns(response);

        var result = await _handler.Handle(
            new GetRemoteStreamContentQuery(_sessionId, "direct-stream", "", "bytes=0-2"),
            CancellationToken.None);

        result.StatusCode.Should().Be(206);
        result.ForwardHeaders.Should().ContainKey("Content-Range");
        result.ForwardHeaders.Should().ContainKey("Accept-Ranges");
        await _peerClient.Received(1).ProxyStreamContentAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            "bytes=0-2");
    }

    [Test]
    public async Task Handle_ShouldNotForwardRange_WhenHlsPath()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("#EXTM3U")
        };

        _peerClient.ProxyStreamContentAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                null)
            .Returns(response);

        await _handler.Handle(
            new GetRemoteStreamContentQuery(
                _sessionId,
                "hls-stream/manifest.m3u8",
                "",
                "bytes=0-2"),
            CancellationToken.None);

        await _peerClient.Received(1).ProxyStreamContentAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            null);
    }
}
