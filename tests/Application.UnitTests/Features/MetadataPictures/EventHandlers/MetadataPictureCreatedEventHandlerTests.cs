using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.EventHandlers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.MetadataPictures.EventHandlers;

/// <summary>
/// Artwork of a media served by a peer is fetched from that peer, so it must run in the federation lane
/// isolated per peer rather than competing for the Metadata lane slots that provider downloads need.
/// </summary>
[TestFixture]
public class MetadataPictureCreatedEventHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private MetadataPictureCreatedEventHandler _handler = null!;

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

        _sender = Substitute.For<ISender>();
        _handler = new MetadataPictureCreatedEventHandler(
            NullLogger<MetadataPictureCreatedEventHandler>.Instance,
            _sender,
            _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldUseFederationLaneWithPeerId_WhenMediaBelongsToAPeer()
    {
        var peerId = Guid.NewGuid();
        var picture = await SeedPictureAsync(MetadataPictureType.Poster, peerId);

        await _handler.Handle(new MetadataPictureCreatedEvent(picture), CancellationToken.None);

        var captured = CapturedTask();
        captured.Should().NotBeNull();
        captured!.Lane.Should().Be(BackgroundTaskLane.Federation);
        captured.FederationPeerId.Should().Be(peerId);
        captured.TriggeredBy.Should().Be(BackgroundTaskTriggeredBy.Federation);
    }

    [Test]
    public async Task Handle_ShouldUseMetadataLane_WhenMediaIsLocal()
    {
        var picture = await SeedPictureAsync(MetadataPictureType.Poster, peerServerId: null);

        await _handler.Handle(new MetadataPictureCreatedEvent(picture), CancellationToken.None);

        var captured = CapturedTask();
        captured.Should().NotBeNull();
        captured!.Lane.Should().Be(BackgroundTaskLane.Metadata);
        captured.FederationPeerId.Should().BeNull();
        captured.TriggeredBy.Should().Be(BackgroundTaskTriggeredBy.System);
    }

    [Test]
    public async Task Handle_ShouldClassifyPosterAsCriticalEnrich()
    {
        var picture = await SeedPictureAsync(MetadataPictureType.Poster, peerServerId: null);

        await _handler.Handle(new MetadataPictureCreatedEvent(picture), CancellationToken.None);

        CapturedTask()!.WorkClass.Should().Be(BackgroundTaskWorkClass.CriticalEnrich);
    }

    [Test]
    public async Task Handle_ShouldClassifySecondaryArtworkAsPolish()
    {
        var picture = await SeedPictureAsync(MetadataPictureType.Backdrop, peerServerId: null);

        await _handler.Handle(new MetadataPictureCreatedEvent(picture), CancellationToken.None);

        CapturedTask()!.WorkClass.Should().Be(BackgroundTaskWorkClass.Polish);
    }

    [Test]
    public async Task Handle_ShouldNotQueueDownload_WhenThumbnailIsGeneratedLocally()
    {
        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = MetadataPictureType.Thumbnail,
            OriginalRemoteUri = null
        };
        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync();

        await _handler.Handle(new MetadataPictureCreatedEvent(picture), CancellationToken.None);

        CapturedTask().Should().BeNull();
    }

    private CreateBackgroundTaskCommand? CapturedTask() =>
        _sender.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<CreateBackgroundTaskCommand>()
            .FirstOrDefault();

    private async Task<MetadataPicture> SeedPictureAsync(MetadataPictureType type, Guid? peerServerId)
    {
        if (peerServerId is Guid peerId)
        {
            _context.PeerServers.Add(new PeerServer
            {
                Id = peerId,
                Name = "Peer",
                BaseUrl = "https://peer.test"
            });
        }

        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            PeerServerId = peerServerId
        };
        _context.Medias.Add(movie);

        var picture = new MetadataPicture
        {
            Id = Guid.NewGuid(),
            Type = type,
            OriginalRemoteUri = new Uri("https://example.test/poster.jpg"),
            MediaId = movie.Id
        };
        _context.MetadataPictures.Add(picture);
        await _context.SaveChangesAsync();

        return picture;
    }
}
