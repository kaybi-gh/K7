using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.Diagnostics.Services;
using K7.Server.Application.Features.Libraries.Commands.RematchLibraryMedia;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Libraries.Commands;

[TestFixture]
public class RematchLibraryMediaCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private IMediaLibraryAvailabilityService _availability = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private RematchLibraryMediaCommandHandler _handler = null!;

    private Guid _libraryId;
    private Guid _groupId;

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

        _groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = _groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = "/media/series",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTasksBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        _availability = Substitute.For<IMediaLibraryAvailabilityService>();
        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();

        _handler = new RematchLibraryMediaCommandHandler(
            _context,
            _sender,
            new OrphanIndexedFileFixBuilder(_context),
            _availability,
            _cacheInvalidator,
            Substitute.For<ILogger<RematchLibraryMediaCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldPassExactFormerMediaIdsIntoCreateMediaTasks_WhenDetaching()
    {
        var mediaA = new Movie { Id = Guid.NewGuid(), Title = "A" };
        var mediaB = new Movie { Id = Guid.NewGuid(), Title = "B" };
        _context.Medias.AddRange(mediaA, mediaB);

        var library = await _context.Libraries.SingleAsync(l => l.Id == _libraryId);
        library.MediaType = LibraryMediaType.Movie;
        library.Title = "Movies";

        var file1 = CreateFile("/media/movies/A.mkv", "A.mkv", mediaA.Id, title: "A");
        var file2 = CreateFile("/media/movies/B.mkv", "B.mkv", mediaB.Id, title: "B");
        _context.IndexedFiles.AddRange(file1, file2);
        await _context.SaveChangesAsync();

        CreateBackgroundTasksBatchCommand? captured = null;
        _sender.Send(Arg.Do<CreateBackgroundTasksBatchCommand>(c => captured = c), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        await _handler.Handle(new RematchLibraryMediaCommand(_libraryId), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Items.Should().HaveCount(2);

        var commands = captured.Items.Select(i => (CreateMediaCommand)i.Request).ToList();
        commands.Should().OnlyContain(c =>
            c.FormerMediaIdsByIndexedFileId != null
            && c.FormerMediaIdsByIndexedFileId.Count == 1);

        var byFileId = commands
            .SelectMany(c => c.FormerMediaIdsByIndexedFileId!)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        byFileId[file1.Id].Should().Be(mediaA.Id);
        byFileId[file2.Id].Should().Be(mediaB.Id);
    }

    [Test]
    public async Task Handle_ShouldDetachAllMediaIds_WhenLibraryHasAttachedFiles()
    {
        var mediaA = new Movie { Id = Guid.NewGuid(), Title = "A" };
        var mediaB = new Movie { Id = Guid.NewGuid(), Title = "B" };
        _context.Medias.AddRange(mediaA, mediaB);

        // Switch library to movie for this detach-focused case.
        var library = await _context.Libraries.SingleAsync(l => l.Id == _libraryId);
        library.MediaType = LibraryMediaType.Movie;
        library.Title = "Movies";

        var file1 = CreateFile("/media/movies/A.mkv", "A.mkv", mediaA.Id, title: "A");
        var file2 = CreateFile("/media/movies/B.mkv", "B.mkv", mediaB.Id, title: "B");
        _context.IndexedFiles.AddRange(file1, file2);
        await _context.SaveChangesAsync();

        var taskCount = await _handler.Handle(new RematchLibraryMediaCommand(_libraryId), CancellationToken.None);

        taskCount.Should().Be(2);
        var files = await _context.IndexedFiles.Where(f => f.LibraryId == _libraryId).ToListAsync();
        files.Should().OnlyContain(f => f.MediaId == null);
        files.Should().OnlyContain(f => f.Identification != null);

        await _availability.Received(1).RebuildForLibraryAsync(_libraryId, Arg.Any<CancellationToken>());
        _cacheInvalidator.Received(1).InvalidateAll();
        await _sender.Received(1).Send(
            Arg.Is<CreateBackgroundTasksBatchCommand>(c =>
                c.Items.Count == 2
                && c.Items.All(t =>
                    t.TriggeredBy == BackgroundTaskTriggeredBy.User
                    && t.Request.GetType() == typeof(CreateMediaCommand)
                    && ((CreateMediaCommand)t.Request).FormerMediaIdsByIndexedFileId != null
                    && ((CreateMediaCommand)t.Request).FormerMediaIdsByIndexedFileId!.Count == 1)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldDetachEverySiblingInFolder_SoConsensusCannotRestick()
    {
        var wrongShow = new Serie { Id = Guid.NewGuid(), Title = "Wrong Show", SortTitle = "Wrong Show" };
        _context.Medias.Add(wrongShow);

        var file1 = CreateSerieFile(
            "/media/series/Show/S01/Show - S01E01.mkv",
            "Show - S01E01.mkv",
            wrongShow.Id,
            "Show",
            1,
            1);
        var file2 = CreateSerieFile(
            "/media/series/Show/S01/Show - S01E02.mkv",
            "Show - S01E02.mkv",
            wrongShow.Id,
            "Show",
            1,
            2);
        _context.IndexedFiles.AddRange(file1, file2);
        await _context.SaveChangesAsync();

        await _handler.Handle(new RematchLibraryMediaCommand(_libraryId), CancellationToken.None);

        var files = await _context.IndexedFiles.Where(f => f.LibraryId == _libraryId).ToListAsync();
        files.Should().HaveCount(2);
        files.Should().OnlyContain(f => f.MediaId == null);
        files.Should().OnlyContain(f => f.Identification!.SeriesTitle == "Show");

        await _sender.Received(1).Send(
            Arg.Is<CreateBackgroundTasksBatchCommand>(c =>
                c.Items.Count == 1
                && c.Items[0].Request.GetType() == typeof(CreateMediaCommand)
                && ((CreateMediaCommand)c.Items[0].Request).IndexedFileIds.Count == 2
                && ((CreateMediaCommand)c.Items[0].Request).MediaType == MediaType.Serie
                && c.Items[0].TriggeredBy == BackgroundTaskTriggeredBy.User
                && ((CreateMediaCommand)c.Items[0].Request).FormerMediaIdsByIndexedFileId != null
                && ((CreateMediaCommand)c.Items[0].Request).FormerMediaIdsByIndexedFileId![file1.Id] == wrongShow.Id
                && ((CreateMediaCommand)c.Items[0].Request).FormerMediaIdsByIndexedFileId![file2.Id] == wrongShow.Id),
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

        var act = async () => await _handler.Handle(
            new RematchLibraryMediaCommand(_libraryId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _sender.DidNotReceive().Send(Arg.Any<CreateBackgroundTasksBatchCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnZero_WhenLibraryHasNoFiles()
    {
        var result = await _handler.Handle(new RematchLibraryMediaCommand(_libraryId), CancellationToken.None);

        result.Should().Be(0);
        await _sender.DidNotReceive().Send(Arg.Any<CreateBackgroundTasksBatchCommand>(), Arg.Any<CancellationToken>());
    }

    private IndexedFile CreateFile(string path, string name, Guid? mediaId, string title) =>
        new()
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = name,
            Extension = ".mkv",
            Path = path,
            ParentDirectory = Path.GetFileName(Path.GetDirectoryName(path)!) ?? "movies",
            Hash = (uint)Random.Shared.Next(),
            Size = 1,
            MediaId = mediaId,
            Identification = new MediaIdentification(title)
        };

    private IndexedFile CreateSerieFile(
        string path,
        string name,
        Guid? mediaId,
        string seriesTitle,
        int season,
        int episode) =>
        new()
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = name,
            Extension = ".mkv",
            Path = path,
            ParentDirectory = Path.GetFileName(Path.GetDirectoryName(path)!) ?? "S01",
            Hash = (uint)Random.Shared.Next(),
            Size = 1,
            MediaId = mediaId,
            Identification = new MediaIdentification($"{seriesTitle} - S{season:00}E{episode:00}")
            {
                SeriesTitle = seriesTitle,
                SeasonNumber = season,
                EpisodeNumber = episode
            }
        };
}
