using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Common.Services;

[TestFixture]
public class MediaAccessFilterTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private MediaAccessFilter _filter = null!;

    private Guid _userId;
    private Guid _visibleMediaId;
    private Guid _excludedMediaId;
    private Guid _libraryExcludedMediaId;
    private Guid _visibleLibraryId;
    private Guid _excludedLibraryId;

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
        _filter = new MediaAccessFilter(_context);

        _userId = Guid.NewGuid();
        _visibleMediaId = Guid.NewGuid();
        _excludedMediaId = Guid.NewGuid();
        _libraryExcludedMediaId = Guid.NewGuid();
        _visibleLibraryId = Guid.NewGuid();
        _excludedLibraryId = Guid.NewGuid();

        var groupId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.AddRange(
            new Library
            {
                Id = _visibleLibraryId,
                LibraryGroupId = groupId,
                MediaType = LibraryMediaType.Movie,
                Title = "Visible",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            },
            new Library
            {
                Id = _excludedLibraryId,
                LibraryGroupId = groupId,
                MediaType = LibraryMediaType.Movie,
                Title = "Excluded",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            });

        _context.Medias.AddRange(
            new Movie { Id = _visibleMediaId, Title = "Visible" },
            new Movie { Id = _excludedMediaId, Title = "Excluded media" },
            new Movie { Id = _libraryExcludedMediaId, Title = "Library excluded" });

        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _visibleMediaId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = _excludedMediaId, LibraryId = _visibleLibraryId },
            new MediaLibraryAvailability { MediaId = _libraryExcludedMediaId, LibraryId = _excludedLibraryId });

        _context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            UserId = _userId,
            MediaId = _excludedMediaId,
            IsSelfExcluded = true
        });
        _context.UserLibraryExclusions.Add(new UserLibraryExclusion
        {
            UserId = _userId,
            LibraryId = _excludedLibraryId,
            IsAdminExcluded = true
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
    public async Task ApplyExclusions_ShouldHideUserExcludedMediaAndExcludedLibraries()
    {
        var results = await _filter
            .ApplyExclusions(_context.Medias, _userId)
            .Select(m => m.Id)
            .ToListAsync();

        results.Should().ContainSingle().Which.Should().Be(_visibleMediaId);
    }

    [Test]
    public async Task GetAccessibleMediaIds_ShouldMatchApplyExclusions()
    {
        var ids = await _filter.GetAccessibleMediaIds(_userId).ToListAsync();

        ids.Should().ContainSingle().Which.Should().Be(_visibleMediaId);
    }
}
