using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MediaUserStateTransferHelperTests
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
    public async Task TransferAsync_ShouldMoveUserMediaState_WhenTargetHasNone()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = fromId,
            PlayCount = 2,
            ProgressPercentage = 40,
            LastPlaybackPosition = 90,
            LastInteractedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(toId);
        state.PlayCount.Should().Be(2);
        state.ProgressPercentage.Should().Be(40);
    }

    [Test]
    public async Task TransferAsync_ShouldMergeUserMediaState_WhenTargetAlreadyHasState()
    {
        var (fromId, toId, userId) = await SeedMoviesAsync();
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = fromId,
            PlayCount = 3,
            ProgressPercentage = 80,
            LastPlaybackPosition = 200,
            IsCompleted = true,
            LastInteractedAt = DateTime.UtcNow
        });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = toId,
            PlayCount = 1,
            ProgressPercentage = 10,
            LastPlaybackPosition = 20,
            LastInteractedAt = DateTime.UtcNow.AddDays(-2)
        });
        await _context.SaveChangesAsync();

        await MediaUserStateTransferHelper.TransferAsync(_context, fromId, toId, _logger);
        await _context.SaveChangesAsync();

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(toId);
        state.PlayCount.Should().Be(3);
        state.ProgressPercentage.Should().Be(80);
        state.IsCompleted.Should().BeTrue();
    }

    private async Task<(Guid FromId, Guid ToId, Guid UserId)> SeedMoviesAsync()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var userId = Guid.NewGuid();

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
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });

        var from = new Movie { Id = Guid.NewGuid(), Title = "Wrong Title" };
        var to = new Movie { Id = Guid.NewGuid(), Title = "Correct Title" };
        _context.Medias.AddRange(from, to);
        await _context.SaveChangesAsync();
        return (from.Id, to.Id, userId);
    }
}
