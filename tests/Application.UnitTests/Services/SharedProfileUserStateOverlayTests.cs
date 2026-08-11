using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class SharedProfileUserStateOverlayTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Guid _sharedProfileId;
    private Guid _userId;
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

        _sharedProfileId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _movieId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Shared Watch" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _movieId,
            ProgressPercentage = 10,
            LastPlaybackPosition = 100,
            LastInteractedAt = DateTime.UtcNow.AddDays(-2),
            IsCompleted = false
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _movieId,
            ProgressPercentage = 35,
            LastPlaybackPosition = 1200,
            LastKnownDurationSeconds = 3600,
            LastInteractedAt = DateTime.UtcNow,
            IsCompleted = false
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
    public async Task ApplyAsync_ShouldReplacePersonalStateWithSharedProfileProgress()
    {
        var movie = await _context.Medias
            .Include(m => m.UserMediaStates)
            .SingleAsync(m => m.Id == _movieId);

        await SharedProfileUserStateOverlay.ApplyAsync(
            _context,
            movie,
            _sharedProfileId,
            _userId);

        movie.UserMediaStates.Should().ContainSingle();
        var state = movie.UserMediaStates.Single();
        state.ProgressPercentage.Should().Be(35);
        state.LastPlaybackPosition.Should().Be(1200);
    }
}
