using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Collections.Queries.GetCollectionItems;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Collections.Queries;

[TestFixture]
public class GetCollectionItemsWithPaginationQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetCollectionItemsWithPaginationQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _libraryId;
    private Guid _collectionId;
    private Guid _availableMediaId;
    private Guid _unavailableMediaId;

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
        _libraryId = Guid.NewGuid();
        _collectionId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
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
            Title = "Lib",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        _availableMediaId = SeedMovie("Available", withFile: true).Id;
        _unavailableMediaId = SeedMovie("Missing", withFile: false).Id;

        _context.Collections.Add(new Collection
        {
            Id = _collectionId,
            Title = "Manual",
            UserId = _userId,
            MediaType = MediaType.Movie
        });
        _context.CollectionItems.AddRange(
            new CollectionItem
            {
                Id = Guid.NewGuid(),
                CollectionId = _collectionId,
                MediaId = _availableMediaId,
                Order = 0
            },
            new CollectionItem
            {
                Id = Guid.NewGuid(),
                CollectionId = _collectionId,
                MediaId = _unavailableMediaId,
                Order = 1
            });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new GetCollectionItemsWithPaginationQueryHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldHideUnavailable_WhenIncludeUnavailableIsFalse()
    {
        var result = await _handler.Handle(new GetCollectionItemsWithPaginationQuery
        {
            CollectionId = _collectionId,
            PageNumber = 1,
            PageSize = 50,
            IncludeUnavailable = false
        }, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.MediaId.Should().Be(_availableMediaId);
    }

    [Test]
    public async Task Handle_ShouldIncludeUnavailable_WhenIncludeUnavailableIsTrue()
    {
        var result = await _handler.Handle(new GetCollectionItemsWithPaginationQuery
        {
            CollectionId = _collectionId,
            PageNumber = 1,
            PageSize = 50,
            IncludeUnavailable = true
        }, CancellationToken.None);

        result.Items.Select(i => i.MediaId).Should().BeEquivalentTo(
            [_availableMediaId, _unavailableMediaId]);
    }

    private Movie SeedMovie(string title, bool withFile)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = title
        };
        if (withFile)
        {
            movie.IndexedFiles.Add(new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = _libraryId,
                MediaId = movie.Id,
                Name = title,
                Extension = ".mkv",
                Path = $"/media/{title}.mkv",
                Hash = 1,
                Size = 1
            });
        }

        _context.Medias.Add(movie);
        return movie;
    }
}
