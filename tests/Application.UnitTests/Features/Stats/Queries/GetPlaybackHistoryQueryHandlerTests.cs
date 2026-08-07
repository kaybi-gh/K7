using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Stats.Queries.GetPlaybackHistory;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Stats.Queries;

[TestFixture]
public class GetPlaybackHistoryQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IIdentityService _identityService = null!;
    private GetPlaybackHistoryQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _otherUserId;
    private Guid _movieId;
    private Guid _sharedProfileId;

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
        _otherUserId = Guid.NewGuid();
        _movieId = Guid.NewGuid();
        _sharedProfileId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _userId, IdentityUserId = "ident", DisplayName = "viewer" },
            new User { Id = _otherUserId, DisplayName = "other" });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Couple",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _identityService = Substitute.For<IIdentityService>();

        _handler = new GetPlaybackHistoryQueryHandler(_context, _currentUser, _identityService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldIncludeSharedProfileSessions_InPersonalHistory()
    {
        var sharedReferenceId = Guid.NewGuid();
        var personalReferenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.AddRange(
            new MediaPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _movieId,
                SessionId = Guid.NewGuid(),
                ReferenceId = sharedReferenceId,
                SharedProfileId = _sharedProfileId,
                SharedProfileNameSnapshot = "Couple",
                StartedAt = now.AddMinutes(-10),
                StoppedAt = now.AddMinutes(-2),
                DurationSeconds = 7200,
                WatchedDurationSeconds = 6000,
                State = PlaybackState.Ended
            },
            new MediaPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _movieId,
                SessionId = Guid.NewGuid(),
                ReferenceId = personalReferenceId,
                StartedAt = now.AddMinutes(-30),
                StoppedAt = now.AddMinutes(-20),
                DurationSeconds = 7200,
                WatchedDurationSeconds = 4000,
                State = PlaybackState.Ended
            });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.ReferenceId == sharedReferenceId && i.SharedProfileName == "Couple");
        result.Items.Should().Contain(i => i.ReferenceId == personalReferenceId && i.SharedProfileName == null);
    }

    [Test]
    public async Task Handle_ShouldIncludeSharedSessions_WhenCoViewer()
    {
        var sharedReferenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _otherUserId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = sharedReferenceId,
            SharedProfileId = _sharedProfileId,
            SharedProfileNameSnapshot = "Couple",
            StartedAt = now.AddMinutes(-10),
            StoppedAt = now.AddMinutes(-2),
            DurationSeconds = 7200,
            WatchedDurationSeconds = 6000,
            State = PlaybackState.Ended
        });
        _context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
        {
            ReferenceId = sharedReferenceId,
            UserId = _userId
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.ReferenceId.Should().Be(sharedReferenceId);
        result.Items[0].SharedProfileName.Should().Be("Couple");
    }

    [Test]
    public async Task Handle_ShouldScopeToSharedProfileOnly_WhenSharedProfileActive()
    {
        var sharedReferenceId = Guid.NewGuid();
        var personalReferenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.AddRange(
            new MediaPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _movieId,
                SessionId = Guid.NewGuid(),
                ReferenceId = sharedReferenceId,
                SharedProfileId = _sharedProfileId,
                SharedProfileNameSnapshot = "Couple",
                StartedAt = now.AddMinutes(-10),
                StoppedAt = now.AddMinutes(-2),
                DurationSeconds = 7200,
                WatchedDurationSeconds = 6000,
                State = PlaybackState.Ended
            },
            new MediaPlaybackSession
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _movieId,
                SessionId = Guid.NewGuid(),
                ReferenceId = personalReferenceId,
                StartedAt = now.AddMinutes(-30),
                StoppedAt = now.AddMinutes(-20),
                DurationSeconds = 7200,
                WatchedDurationSeconds = 4000,
                State = PlaybackState.Ended
            });
        await _context.SaveChangesAsync();

        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(_sharedProfileId);

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().ContainSingle()
            .Which.ReferenceId.Should().Be(sharedReferenceId);
    }

    [Test]
    public async Task Handle_ShouldNotUseMediaDuration_AsWatchedFallback()
    {
        var referenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = referenceId,
            StartedAt = now.AddMinutes(-5),
            DurationSeconds = 7200,
            PositionSeconds = 0,
            WatchedDurationSeconds = 0,
            State = PlaybackState.Playing
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].TotalWatchedSeconds.Should().Be(0);
        result.Items[0].IsCompleted.Should().BeFalse();
        result.Items[0].IsSkipped.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMarkSkipped_WhenFinishedWithLittleProgress()
    {
        var referenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = referenceId,
            StartedAt = now.AddMinutes(-2),
            StoppedAt = now,
            DurationSeconds = 200,
            PositionSeconds = 8,
            WatchedDurationSeconds = 8,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].IsCompleted.Should().BeFalse();
        result.Items[0].IsSkipped.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldNotMarkSkipped_WhenIncompleteWithMeaningfulProgress()
    {
        var referenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = referenceId,
            StartedAt = now.AddMinutes(-5),
            StoppedAt = now,
            DurationSeconds = 200,
            PositionSeconds = 90,
            WatchedDurationSeconds = 90,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].IsCompleted.Should().BeFalse();
        result.Items[0].IsSkipped.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMarkCompleted_WhenCompletedAtSet()
    {
        var referenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = referenceId,
            StartedAt = now.AddMinutes(-5),
            StoppedAt = now,
            DurationSeconds = 200,
            PositionSeconds = 200,
            WatchedDurationSeconds = 200,
            CompletedAt = now,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPlaybackHistoryQuery { Period = "all" }, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].IsCompleted.Should().BeTrue();
        result.Items[0].IsSkipped.Should().BeFalse();
        result.Items[0].TotalWatchedSeconds.Should().Be(200);
    }
}

