using K7.Server.Application.Common.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class HomeRecommendationServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private HomeRecommendationService _service = null!;
    private Guid _userId;
    private Guid _seedMovieId;
    private Guid _candidateMovieId;

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
        _seedMovieId = Guid.NewGuid();
        _candidateMovieId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "viewer" });
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
            MediaType = LibraryMediaType.Movie,
            Title = "Movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.Medias.AddRange(
            new Movie { Id = _seedMovieId, Title = "Seed" },
            new Movie { Id = _candidateMovieId, Title = "Candidate" });
        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { MediaId = _seedMovieId, LibraryId = libraryId },
            new MediaLibraryAvailability { MediaId = _candidateMovieId, LibraryId = libraryId });
        _context.SaveChanges();

        _service = new HomeRecommendationService(_context, new MediaAccessFilter(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task GetRecommendedMediaIdsAsync_ShouldIgnoreIncompleteSessions()
    {
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _seedMovieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            StoppedAt = DateTime.UtcNow.AddMinutes(-4),
            DurationSeconds = 7200,
            WatchedDurationSeconds = 5,
            State = PlaybackState.Ended
        });
        AddRecommendationAndCandidateExternalId();
        await _context.SaveChangesAsync();

        var result = await _service.GetRecommendedMediaIdsAsync(_userId, null, 1, 20);

        result.Should().BeEmpty();
    }

    [Test]
    public async Task GetRecommendedMediaIdsAsync_ShouldSeedFromCompletedSessions()
    {
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _seedMovieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1),
            DurationSeconds = 7200,
            WatchedDurationSeconds = 6500,
            State = PlaybackState.Ended
        });
        AddRecommendationAndCandidateExternalId();
        await _context.SaveChangesAsync();

        var result = await _service.GetRecommendedMediaIdsAsync(_userId, null, 1, 20);

        result.Should().Equal(_candidateMovieId);
    }

    [Test]
    public async Task GetRecommendedMediaIdsAsync_ShouldIgnoreIncompleteUserMediaStates()
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            MediaId = _seedMovieId,
            LastInteractedAt = DateTime.UtcNow,
            IsCompleted = false,
            ProgressPercentage = 1
        });
        AddRecommendationAndCandidateExternalId();
        await _context.SaveChangesAsync();

        var result = await _service.GetRecommendedMediaIdsAsync(_userId, null, 1, 20);

        result.Should().BeEmpty();
    }

    private void AddRecommendationAndCandidateExternalId()
    {
        _context.MediaRecommendations.Add(new MediaRecommendation
        {
            MediaId = _seedMovieId,
            ProviderName = "tmdb",
            RecommendedIds = ["ext-candidate"]
        });
        _context.ExternalIds.Add(new ExternalId
        {
            Id = Guid.NewGuid(),
            MediaId = _candidateMovieId,
            ProviderName = "tmdb",
            Value = "ext-candidate"
        });
    }
}
