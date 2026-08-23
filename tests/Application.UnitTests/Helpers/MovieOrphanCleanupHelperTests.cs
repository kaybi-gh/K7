using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MovieOrphanCleanupHelperTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ILogger _logger = null!;

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
        _logger = Substitute.For<ILogger>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldDeleteMovie_WhenNoFilesAndNoUserData()
    {
        var movieId = await SeedOrphanMovieAsync();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == movieId)).Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepMovie_WhenIndexedFileRemains()
    {
        var movieId = await SeedOrphanMovieAsync(withFile: true);

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);

        deleted.Should().BeFalse();
        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == movieId)).Should().BeTrue();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepMovie_WhenCollectionItemExists()
    {
        var movieId = await SeedOrphanMovieAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        var collection = new Collection { Id = Guid.NewGuid(), Title = "Favs", UserId = userId };
        _context.Collections.Add(collection);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = movieId,
            Order = 0
        });
        await _context.SaveChangesAsync();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepMovie_WhenPlaybackSessionExists()
    {
        var movieId = await SeedOrphanMovieAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            UserId = userId,
            MediaId = movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            PositionSeconds = 1,
            DurationSeconds = 10,
            WatchedDurationSeconds = 1,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepMovie_WhenExclusionExists()
    {
        var movieId = await SeedOrphanMovieAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = userId,
            MediaId = movieId,
            IsAdminExcluded = true
        });
        await _context.SaveChangesAsync();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldKeepMovie_WhenUserMediaStateExists()
    {
        var movieId = await SeedOrphanMovieAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = movieId,
            PlayCount = 1
        });
        await _context.SaveChangesAsync();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldDeleteMovie_WhenLibraryAvailabilityRemains()
    {
        var movieId = await SeedOrphanMovieAsync();
        var libraryId = await _context.Libraries.Select(l => l.Id).SingleAsync();
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = libraryId,
            MediaId = movieId
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == movieId)).Should().BeFalse();
        (await _context.MediaLibraryAvailabilities.AnyAsync(a => a.MediaId == movieId)).Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldDeleteMovie_WhenExternalIdsAndPicturesRemain()
    {
        var movieId = await SeedOrphanMovieAsync();
        _context.ExternalIds.Add(new ExternalId
        {
            ProviderName = "tmdb",
            Value = "42",
            MediaId = movieId
        });
        _context.MetadataPictures.Add(new MetadataPicture
        {
            Type = MetadataPictureType.Poster,
            MediaId = movieId,
            LocalPath = "/meta/poster.jpg"
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == movieId)).Should().BeFalse();
        (await _context.ExternalIds.AnyAsync(e => e.MediaId == movieId)).Should().BeFalse();
        (await _context.MetadataPictures.AnyAsync(p => p.MediaId == movieId)).Should().BeFalse();
    }

    [Test]
    public async Task TryDeleteIfOrphanAsync_ShouldDeleteMovie_WhenPlaybackBookmarkRemainsWithoutOtherUserData()
    {
        var movieId = await SeedOrphanMovieAsync();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.PlaybackBookmarks.Add(new ItemPlaybackBookmark
        {
            UserId = userId,
            MediaId = movieId,
            PositionSeconds = 12,
            DurationSeconds = 100,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var deleted = await MovieOrphanCleanupHelper.TryDeleteIfOrphanAsync(_context, movieId, _logger);
        await _context.SaveChangesAsync();

        deleted.Should().BeTrue();
        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == movieId)).Should().BeFalse();
        (await _context.PlaybackBookmarks.OfType<ItemPlaybackBookmark>().AnyAsync(b => b.MediaId == movieId))
            .Should().BeFalse();
    }

    private async Task<Guid> SeedOrphanMovieAsync(bool withFile = false)
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
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
            RootPath = "/media/movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Orphan",
            IndexedFiles = []
        };
        _context.Medias.Add(movie);

        if (withFile)
        {
            var file = new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = libraryId,
                Name = "Orphan.mkv",
                Extension = ".mkv",
                Path = "/media/movies/Orphan.mkv",
                ParentDirectory = "movies",
                Hash = 1,
                Size = 1,
                MediaId = movie.Id
            };
            movie.IndexedFiles.Add(file);
            _context.IndexedFiles.Add(file);
        }

        await _context.SaveChangesAsync();
        return movie.Id;
    }
}
