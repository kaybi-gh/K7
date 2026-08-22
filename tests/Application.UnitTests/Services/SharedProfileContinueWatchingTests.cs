using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using K7.Tests.Helpers.Samples;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

/// <summary>
/// Verifies the continue-watching query filter branches on shared-profile playback bookmarks.
/// </summary>
[TestFixture]
public class SharedProfileContinueWatchingTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private Guid _sharedProfileId;
    private Guid _hostUserId;
    private Guid _memberUserId;

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

        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public void WhereEligibleForSharedProfileContinueWatching_ShouldReturnOnlyMediaWithSharedProfileProgress()
    {
        var eligibleMediaId = Guid.NewGuid();
        var otherMediaId = Guid.NewGuid();
        _context.Medias.AddRange(
            new Movie { Id = eligibleMediaId, Title = "Ongoing" },
            new Movie { Id = otherMediaId, Title = "Not started" });
        var (libraryId, peerServerId) = RemoteIndexedFilesSamples.EnsureLibraryAndPeer(_context);
        _context.RemoteIndexedFiles.Add(RemoteIndexedFilesSamples.Create(eligibleMediaId, libraryId, peerServerId));

        var utcNow = DateTime.UtcNow;
        _context.PlaybackBookmarks.Add(new ItemPlaybackBookmark
        {
            SharedProfileId = _sharedProfileId,
            MediaId = eligibleMediaId,
            PositionSeconds = 600,
            DurationSeconds = 3600,
            UpdatedAt = utcNow.AddHours(-1)
        });

        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _memberUserId,
            MediaId = otherMediaId,
            LastInteractedAt = utcNow.AddHours(-1),
            IsCompleted = false
        });
        _context.SaveChanges();

        var policy = new VideoPlaybackPolicySettingsDto
        {
            MinResumePercent = 5,
            MinResumeDurationSeconds = 300,
            CompletedThresholdPercent = 90,
            ContinueWatchingMaxAgeDays = 14
        };

        var results = _context.Medias
            .AsNoTracking()
            .WhereEligibleForSharedProfileContinueWatching(_context, _sharedProfileId, policy, utcNow)
            .Select(m => m.Id)
            .ToList();

        results.Should().ContainSingle().Which.Should().Be(eligibleMediaId);
    }

    [Test]
    public void WhereEligibleForSharedProfileContinueWatching_ShouldExcludeItemsWithoutBookmarks()
    {
        var completedMediaId = Guid.NewGuid();
        _context.Medias.Add(new Movie { Id = completedMediaId, Title = "Watched" });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = completedMediaId,
            LastInteractedAt = DateTime.UtcNow,
            IsCompleted = true
        });
        _context.SaveChanges();

        var policy = new VideoPlaybackPolicySettingsDto
        {
            MinResumePercent = 5,
            MinResumeDurationSeconds = 300,
            CompletedThresholdPercent = 90,
            ContinueWatchingMaxAgeDays = 14
        };

        var results = _context.Medias
            .AsNoTracking()
            .WhereEligibleForSharedProfileContinueWatching(_context, _sharedProfileId, policy, DateTime.UtcNow)
            .Select(m => m.Id)
            .ToList();

        results.Should().BeEmpty();
    }
}
