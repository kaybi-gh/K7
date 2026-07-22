using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Stats.Queries.GetWatchStats;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Stats.Queries;

[TestFixture]
public class GetWatchStatsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetWatchStatsQueryHandler _handler = null!;
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

        _userId = Guid.NewGuid();
        _movieId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "viewer" });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _handler = new GetWatchStatsQueryHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldIgnoreSessionsWithoutCompletedAt()
    {
        var incompleteReferenceId = Guid.NewGuid();
        var completedReferenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.AddRange(
            new MediaPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _movieId,
                SessionId = Guid.NewGuid(),
                ReferenceId = incompleteReferenceId,
                StartedAt = now.AddMinutes(-10),
                StoppedAt = now.AddMinutes(-9),
                DurationSeconds = 7200,
                WatchedDurationSeconds = 5,
                State = PlaybackState.Ended
            },
            new MediaPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _movieId,
                SessionId = Guid.NewGuid(),
                ReferenceId = completedReferenceId,
                StartedAt = now.AddMinutes(-5),
                CompletedAt = now.AddMinutes(-1),
                DurationSeconds = 7200,
                WatchedDurationSeconds = 6500,
                State = PlaybackState.Ended
            });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetWatchStatsQuery(Period: "all"), CancellationToken.None);

        result.TotalPlays.Should().Be(1);
        result.UniqueItemsPlayed.Should().Be(1);
        result.TotalWatchTimeHours.Should().Be(Math.Round(6500 / 3600.0, 1));
        result.TopItems.Should().ContainSingle(i => i.Id == _movieId && i.PlayCount == 1);
    }

    [Test]
    public async Task Handle_ShouldReturnEmpty_WhenOnlyIncompleteSessionsExist()
    {
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            StoppedAt = DateTime.UtcNow.AddMinutes(-1),
            DurationSeconds = 7200,
            WatchedDurationSeconds = 5,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetWatchStatsQuery(Period: "all"), CancellationToken.None);

        result.TotalPlays.Should().Be(0);
        result.UniqueItemsPlayed.Should().Be(0);
        result.TotalWatchTimeHours.Should().Be(0);
        result.TopItems.Should().BeEmpty();
    }
}
