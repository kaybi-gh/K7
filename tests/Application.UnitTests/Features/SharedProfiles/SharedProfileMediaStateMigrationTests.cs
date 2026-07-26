using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles;

[TestFixture]
public class SharedProfileMediaStateMigrationTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Guid _sharedProfileId;
    private Guid _hostUserId;
    private Guid _memberUserId;
    private Guid _mediaId;

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
        _mediaId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _hostUserId, DisplayName = "host" },
            new User { Id = _memberUserId, DisplayName = "member" });
        _context.Medias.Add(new Movie { Id = _mediaId, Title = "Shared watch" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Couple",
            HostUserId = _hostUserId,
            CreatedByUserId = _hostUserId
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
    public async Task MigrateToMembersAsync_ShouldCreatePersonalStates_WhenNoneExist()
    {
        var interactedAt = DateTime.UtcNow.AddHours(-2);
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _mediaId,
            ProgressPercentage = 35,
            LastPlaybackPosition = 700,
            LastKnownDurationSeconds = 2000,
            LastInteractedAt = interactedAt,
            IsCompleted = false,
            PlayCount = 1,
            ExcludedFromContinueWatching = false
        });
        await _context.SaveChangesAsync();

        await SharedProfileMediaStateMigration.MigrateToMembersAsync(
            _context,
            _sharedProfileId,
            [_hostUserId, _memberUserId],
            CancellationToken.None);
        await _context.SaveChangesAsync();

        var states = await _context.UserMediaStates.AsNoTracking().ToListAsync();
        states.Should().HaveCount(2);
        states.Should().OnlyContain(s =>
            s.MediaId == _mediaId
            && s.ProgressPercentage == 35
            && s.LastPlaybackPosition == 700
            && s.LastInteractedAt == interactedAt
            && s.PlayCount == 1);
        states.Select(s => s.UserId).Should().BeEquivalentTo([_hostUserId, _memberUserId]);
    }

    [Test]
    public async Task MigrateToMembersAsync_ShouldPreferNewerSharedProgress_WhenPersonalExists()
    {
        var olderPersonalAt = DateTime.UtcNow.AddDays(-3);
        var newerSharedAt = DateTime.UtcNow.AddHours(-1);

        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _memberUserId,
            MediaId = _mediaId,
            ProgressPercentage = 10,
            LastPlaybackPosition = 100,
            LastInteractedAt = olderPersonalAt,
            PlayCount = 1,
            IsCompleted = false
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _mediaId,
            ProgressPercentage = 55,
            LastPlaybackPosition = 1100,
            LastInteractedAt = newerSharedAt,
            PlayCount = 3,
            IsCompleted = false
        });
        await _context.SaveChangesAsync();

        await SharedProfileMediaStateMigration.MigrateToMembersAsync(
            _context,
            _sharedProfileId,
            [_memberUserId],
            CancellationToken.None);
        await _context.SaveChangesAsync();

        var personal = await _context.UserMediaStates.SingleAsync(s => s.UserId == _memberUserId);
        personal.ProgressPercentage.Should().Be(55);
        personal.LastPlaybackPosition.Should().Be(1100);
        personal.LastInteractedAt.Should().Be(newerSharedAt);
        personal.PlayCount.Should().Be(3);
    }

    [Test]
    public async Task MigrateToMembersAsync_ShouldKeepNewerPersonalProgress_WhenSharedIsOlder()
    {
        var newerPersonalAt = DateTime.UtcNow.AddHours(-1);
        var olderSharedAt = DateTime.UtcNow.AddDays(-2);

        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _memberUserId,
            MediaId = _mediaId,
            ProgressPercentage = 80,
            LastPlaybackPosition = 1600,
            LastInteractedAt = newerPersonalAt,
            PlayCount = 2,
            IsCompleted = false
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _mediaId,
            ProgressPercentage = 20,
            LastPlaybackPosition = 400,
            LastInteractedAt = olderSharedAt,
            PlayCount = 5,
            IsCompleted = false
        });
        await _context.SaveChangesAsync();

        await SharedProfileMediaStateMigration.MigrateToMembersAsync(
            _context,
            _sharedProfileId,
            [_memberUserId],
            CancellationToken.None);
        await _context.SaveChangesAsync();

        var personal = await _context.UserMediaStates.SingleAsync(s => s.UserId == _memberUserId);
        personal.ProgressPercentage.Should().Be(80);
        personal.LastPlaybackPosition.Should().Be(1600);
        personal.LastInteractedAt.Should().Be(newerPersonalAt);
        personal.PlayCount.Should().Be(5);
    }
}
