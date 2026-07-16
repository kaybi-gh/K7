using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Federation.Commands.CreateFederationStreamSession;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Devices;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Federation.Commands;

[TestFixture]
public class CreateFederationStreamSessionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private PeerAuthorizationService _peerAuthorization = null!;
    private ISender _sender = null!;
    private ActiveStreamTracker _streamTracker = null!;
    private CreateFederationStreamSessionCommandHandler _handler = null!;

    private Guid _peerId;
    private Guid _libraryId;
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
        _libraryId = Guid.NewGuid();
        _indexedFileId = Guid.NewGuid();
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
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.PeerShareAgreements.Add(new PeerShareAgreement
        {
            PeerServerId = _peerId,
            LibraryId = _libraryId,
            Direction = ShareDirection.Outbound,
            IsEnabled = true
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = _indexedFileId,
            LibraryId = _libraryId,
            Name = "song",
            Extension = ".mp3",
            Path = "/media/song.mp3",
            Hash = 1,
            Size = 1,
            FileMetadata = new AudioFileMetadata
            {
                Id = metadataId,
                Container = "mp3",
                Duration = TimeSpan.FromMinutes(4),
                AudioTrack = new AudioFileTrack
                {
                    Index = 0,
                    Codec = "mp3",
                    Channels = 2
                }
            }
        });
        _context.SaveChanges();

        _peerAuthorization = new PeerAuthorizationService(
            _context,
            Substitute.For<IFederationViewerAssertionService>(),
            Substitute.For<IPeerClient>());
        _sender = Substitute.For<ISender>();
        _streamTracker = new ActiveStreamTracker();
        _handler = new CreateFederationStreamSessionCommandHandler(
            _peerAuthorization,
            _context,
            _sender,
            _streamTracker);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateAudioSessionWithDirectStream_WhenFileAccessible()
    {
        var capabilities = CreateCapabilities(["audio-mp3-mp3"]);

        var result = await _handler.Handle(new CreateFederationStreamSessionCommand(
            InboundClientId,
            new CreateFederationStreamSessionRequest
            {
                IndexedFileId = _indexedFileId,
                DeviceCapabilities = capabilities,
                AudioTrackIndex = 0
            }), CancellationToken.None);

        result.Session.Id.Should().NotBeEmpty();
        result.Session.IndexedFileId.Should().Be(_indexedFileId);
        result.Session.Source.Should().NotBeNull();
        result.Session.Source!.MimeType.Should().Be("audio/mpeg");
        result.Location.Should().Be($"/api/federation/stream-sessions/{result.Session.Id}");

        var persisted = await _context.StreamSessions.SingleAsync(s => s.Id == result.Session.Id);
        persisted.PeerServerId.Should().Be(_peerId);
        persisted.IndexedFileId.Should().Be(_indexedFileId);

        _streamTracker.GetStreamInfo(result.Session.Id).Should().NotBeNull();
        await _sender.DidNotReceive().Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldQueueHlsSegments_WhenVideoFileHasNoSegments()
    {
        var videoFileId = Guid.NewGuid();
        var videoMetadataId = Guid.NewGuid();
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = videoFileId,
            LibraryId = _libraryId,
            Name = "movie",
            Extension = ".mkv",
            Path = "/media/movie.mkv",
            Hash = 2,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = videoMetadataId,
                Container = "matroska",
                VideoBitrate = 5_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Duration = TimeSpan.FromHours(2),
                AudioTracks =
                [
                    new AudioFileTrack
                    {
                        Index = 0,
                        Codec = "aac",
                        Channels = 2,
                        IsDefault = true
                    }
                ],
                VideoTracks =
                [
                    new VideoFileTrack
                    {
                        Index = 0,
                        Codec = "h264",
                        Width = 1920,
                        Height = 1080,
                        Profile = "high",
                        Level = 40
                    }
                ]
            }
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new CreateFederationStreamSessionCommand(
            InboundClientId,
            new CreateFederationStreamSessionRequest
            {
                IndexedFileId = videoFileId,
                DeviceCapabilities = CreateCapabilities(["audio-mp4-aac", "video-mp4-aac-h264"]),
                AudioTrackIndex = 0
            }), CancellationToken.None);

        result.Session.Source.Should().NotBeNull();
        result.Session.Source!.MimeType.Should().Be("application/vnd.apple.mpegurl");

        await _sender.Received(1).Send(
            Arg.Is<CreateBackgroundTaskCommand>(c => c.Request.GetType() == typeof(ComputeHlsSegmentsCommand)),
            Arg.Any<CancellationToken>());
    }

    private static DevicePlaybackCapabilitiesDto CreateCapabilities(IEnumerable<string> formatIds) => new()
    {
        SupportedMediaFormats = formatIds.Select(CreateMediaFormat).ToList(),
        SupportedSubtitlesCodecs = [],
        SupportsHDR = false
    };

    private static MediaFormatDto CreateMediaFormat(string id)
    {
        var parts = id.Split('-');
        if (parts[0] == "audio")
        {
            return new AudioMediaFormatDto
            {
                Id = id,
                Container = parts[1],
                Codec = parts[2]
            };
        }

        return new VideoMediaFormatDto
        {
            Id = id,
            Container = parts[1],
            AudioCodec = parts[2],
            VideoCodec = parts[3]
        };
    }
}
