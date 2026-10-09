using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Features.Libraries.Commands.RefreshLibraryMetadata;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Libraries.Commands;

[TestFixture]
public class RefreshLibraryMetadataCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private RefreshLibraryMetadataCommandHandler _handler = null!;

    private Guid _libraryId;
    private Guid _movieId;

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

        var groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _movieId = Guid.NewGuid();

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
            RootPath = "/media/movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film", SortTitle = "Film" });
        _context.Medias.Add(new Movie { Id = Guid.NewGuid(), Title = "Other", SortTitle = "Other" });
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = _libraryId,
            MediaId = _movieId
        });
        _context.SaveChanges();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<QueueRefreshMediaMetadataCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        _handler = new RefreshLibraryMetadataCommandHandler(
            _context,
            _sender,
            Substitute.For<ILogger<RefreshLibraryMetadataCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldQueueRefresh_OnlyForMediaLinkedToTheLibrary()
    {
        var queued = await _handler.Handle(new RefreshLibraryMetadataCommand(_libraryId), CancellationToken.None);

        queued.Should().Be(1);
        await _sender.Received(1).Send(
            Arg.Is<QueueRefreshMediaMetadataCommand>(c => c.MediaId == _movieId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldThrowValidation_WhenLibraryIsFederated()
    {
        var peer = PeerServer.CreatePending("Peer", "https://peer.example", "token");
        _context.PeerServers.Add(peer);
        var library = await _context.Libraries.SingleAsync(l => l.Id == _libraryId);
        library.PeerServerId = peer.Id;
        await _context.SaveChangesAsync();

        var act = async () => await _handler.Handle(new RefreshLibraryMetadataCommand(_libraryId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _sender.DidNotReceive().Send(Arg.Any<QueueRefreshMediaMetadataCommand>(), Arg.Any<CancellationToken>());
    }
}
