using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class SharedProfileMediaStateUpdaterTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IPlaybackPolicySettingsProvider _policies = null!;
    private SharedProfileMediaStateUpdater _updater = null!;
    private Guid _sharedProfileId;
    private Guid _hostUserId;
    private Guid _memberUserId;
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
        _hostUserId = Guid.NewGuid();
        _memberUserId = Guid.NewGuid();
        _movieId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _hostUserId, DisplayName = "host" },
            new User { Id = _memberUserId, DisplayName = "member" });

        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Family",
            HostUserId = _hostUserId,
            CreatedByUserId = _hostUserId
        });

        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.SaveChanges();

        _policies = Substitute.For<IPlaybackPolicySettingsProvider>();
        _policies.GetEffectiveVideoPolicyAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new VideoPlaybackPolicySettingsDto
            {
                CompletedThresholdPercent = 90,
                MinResumePercent = 5,
                MinResumeDurationSeconds = 300,
                ContinueWatchingMaxAgeDays = 14
            });
        _policies.GetEffectiveAudioPolicyAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new AudioPlaybackPolicySettingsDto
            {
                CompletedThresholdPercent = 50,
                CompletedMinDurationSeconds = 240
            });

        _updater = new SharedProfileMediaStateUpdater(_context, _policies);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ApplyAsync_ShouldCreateSharedProfileMediaState_WhenNoneExists()
    {
        var media = await _context.Medias.SingleAsync(m => m.Id == _movieId);

        var result = await _updater.ApplyAsync(
            _sharedProfileId, media, _movieId, position: 50, duration: 100, DateTime.UtcNow);

        await _context.SaveChangesAsync();

        var states = await _context.SharedProfileMediaStates.ToListAsync();
        states.Should().HaveCount(1);
        states[0].SharedProfileId.Should().Be(_sharedProfileId);
        states[0].MediaId.Should().Be(_movieId);
        result.ProgressPercentage.Should().BeApproximately(50, 0.01);
        result.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task ApplyAsync_ShouldNotWriteAnyUserMediaState_ForHostOrMembers()
    {
        var media = await _context.Medias.SingleAsync(m => m.Id == _movieId);

        await _updater.ApplyAsync(
            _sharedProfileId, media, _movieId, position: 95, duration: 100, DateTime.UtcNow);

        await _context.SaveChangesAsync();

        var userStates = await _context.UserMediaStates.ToListAsync();
        userStates.Should().BeEmpty();
    }

    [Test]
    public async Task ApplyAsync_ShouldMarkCompleted_WhenPastThreshold()
    {
        var media = await _context.Medias.SingleAsync(m => m.Id == _movieId);

        var result = await _updater.ApplyAsync(
            _sharedProfileId, media, _movieId, position: 91, duration: 100, DateTime.UtcNow);

        await _context.SaveChangesAsync();

        result.IsCompleted.Should().BeTrue();
        result.WasNewlyCompleted.Should().BeTrue();

        var state = await _context.SharedProfileMediaStates.SingleAsync();
        state.IsCompleted.Should().BeTrue();
        state.PlayCount.Should().Be(1);
    }
}
