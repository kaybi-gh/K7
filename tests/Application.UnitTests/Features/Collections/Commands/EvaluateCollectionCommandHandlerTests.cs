using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Collections.Commands.EvaluateCollection;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Collections.Commands;

[TestFixture]
public class EvaluateCollectionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private EvaluateCollectionCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _groupA;
    private Guid _groupB;
    private Guid _libraryA;
    private Guid _libraryB;

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

        _userId = Guid.NewGuid();
        _groupA = Guid.NewGuid();
        _groupB = Guid.NewGuid();
        _libraryA = Guid.NewGuid();
        _libraryB = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.LibraryGroups.AddRange(
            new LibraryGroup { Id = _groupA, Title = "A", MediaType = LibraryMediaType.Movie },
            new LibraryGroup { Id = _groupB, Title = "B", MediaType = LibraryMediaType.Movie });
        _context.Libraries.AddRange(
            new Library
            {
                Id = _libraryA,
                LibraryGroupId = _groupA,
                Title = "LibA",
                MediaType = LibraryMediaType.Movie,
                RootPath = "/a",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            },
            new Library
            {
                Id = _libraryB,
                LibraryGroupId = _groupB,
                Title = "LibB",
                MediaType = LibraryMediaType.Movie,
                RootPath = "/b",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new EvaluateCollectionCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldOnlyIncludeMediaFromLibraryGroup_WhenScoped()
    {
        var inGroup = SeedMovie("In", _libraryA);
        SeedMovie("Out", _libraryB);

        var collectionId = Guid.NewGuid();
        _context.Collections.Add(new Collection
        {
            Id = collectionId,
            Title = "Scoped",
            UserId = _userId,
            MediaType = MediaType.Movie,
            LibraryGroupId = _groupA,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All, Items = [] }
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new EvaluateCollectionCommand { Id = collectionId }, CancellationToken.None);

        var collection = await _context.Collections
            .Include(c => c.Items)
            .SingleAsync(c => c.Id == collectionId);
        collection.Items.Should().ContainSingle().Which.MediaId.Should().Be(inGroup.Id);
        collection.LastEvaluatedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenNotOwner()
    {
        var collectionId = Guid.NewGuid();
        _context.Collections.Add(new Collection
        {
            Id = collectionId,
            Title = "Mine",
            UserId = _userId,
            MediaType = MediaType.Movie,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All, Items = [] }
        });
        await _context.SaveChangesAsync();

        _currentUser.Id.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new EvaluateCollectionCommand { Id = collectionId }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private Movie SeedMovie(string title, Guid libraryId)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = title,
            Created = DateTime.UtcNow
        };
        movie.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            MediaId = movie.Id,
            Name = title,
            Extension = ".mkv",
            Path = $"/media/{title}.mkv",
            Hash = 1,
            Size = 1
        });
        _context.Medias.Add(movie);
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            MediaId = movie.Id,
            LibraryId = libraryId
        });
        return movie;
    }
}
