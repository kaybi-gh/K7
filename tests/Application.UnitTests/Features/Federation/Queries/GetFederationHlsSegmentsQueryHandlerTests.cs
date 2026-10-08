using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Queries.GetFederationHlsSegments;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Queries;

[TestFixture]
public class GetFederationHlsSegmentsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private PeerAuthorizationService _peerAuthorization = null!;
    private GetFederationHlsSegmentsQueryHandler _handler = null!;

    private Guid _peerId;
    private Guid _sessionId;
    private Guid _indexedFileId;
    private const string InboundClientId = "federation-peer";

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

        _peerId = Guid.NewGuid();
        _sessionId = Guid.NewGuid();
        _indexedFileId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

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
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = _indexedFileId,
            LibraryId = libraryId,
            Name = "movie",
            Extension = ".mkv",
            Path = "/media/movie.mkv",
            Hash = 1,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = metadataId,
                Container = "matroska",
                VideoBitrate = 5_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Duration = TimeSpan.FromMinutes(10)
            }
        });
        _context.StreamSessions.Add(new StreamSession
        {
            Id = _sessionId,
            IndexedFileId = _indexedFileId,
            PeerServerId = _peerId,
            FederatedPlaybackExecution = FederatedPlaybackExecution.Peer,
            PlaybackSettingsJson = "{}"
        });
        _context.SaveChanges();

        _peerAuthorization = new PeerAuthorizationService(
            _context,
            Substitute.For<IFederationViewerAssertionService>(),
            Substitute.For<IPeerClient>());
        _handler = new GetFederationHlsSegmentsQueryHandler(_peerAuthorization, _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnEmptyList_WhenGridMissing()
    {
        var result = await _handler.Handle(
            new GetFederationHlsSegmentsQuery(InboundClientId, _sessionId),
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReturnOrderedSegments_WhenGridPresent()
    {
        var metadataId = await _context.IndexedFiles
            .Where(f => f.Id == _indexedFileId)
            .Select(f => f.FileMetadata!.Id)
            .SingleAsync();

        _context.HlsSegments.AddRange(
            new HlsSegment
            {
                FileMetadataId = metadataId,
                IndexedFileId = _indexedFileId,
                Number = 1,
                StartTimestamp = 6000,
                Duration = 6000
            },
            new HlsSegment
            {
                FileMetadataId = metadataId,
                IndexedFileId = _indexedFileId,
                Number = 0,
                StartTimestamp = 0,
                Duration = 6000
            });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetFederationHlsSegmentsQuery(InboundClientId, _sessionId),
            CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Number.Should().Be(0);
        result[1].Number.Should().Be(1);
        result[0].StartTimestamp.Should().Be(0);
        result[1].Duration.Should().Be(6000);
    }
}
