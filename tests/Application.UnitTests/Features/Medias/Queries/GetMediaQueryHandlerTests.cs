using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Medias.Queries.GetMedia;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class GetMediaQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetMediaQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _episodeId;

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
        _episodeId = Guid.NewGuid();
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        var serie = new Serie { Id = serieId, Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = seasonId,
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        serie.Seasons.Add(season);
        var episode = new SerieEpisode
        {
            Id = _episodeId,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = season,
            EpisodeNumber = 1,
            Title = "E1",
            SortTitle = "E1"
        };
        season.Episodes.Add(episode);

        _context.Medias.AddRange(serie, season, episode);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = _episodeId,
            IsCompleted = false,
            LastInteractedAt = DateTime.UtcNow
        });
        _context.PlaybackBookmarks.Add(new ItemPlaybackBookmark
        {
            UserId = _userId,
            MediaId = _episodeId,
            PositionSeconds = 333,
            DurationSeconds = 1800,
            UpdatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var currentUser = Substitute.For<IUser>();
        currentUser.Id.Returns(_userId);
        currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var accessGuard = Substitute.For<IMediaAccessGuard>();
        var bookmarkService = new PlaybackBookmarkService(
            _context,
            NullLogger<PlaybackBookmarkService>.Instance);

        _handler = new GetMediaQueryHandler(_context, currentUser, accessGuard, bookmarkService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnItemBookmark_SoResumePositionIsMapped()
    {
        var result = await _handler.Handle(new GetMediaQuery(_episodeId), CancellationToken.None);

        result.ItemBookmarks.Should().ContainKey(_episodeId);
        result.ItemBookmarks[_episodeId].PositionSeconds.Should().Be(333);

        var dto = (SerieEpisodeDto)result.Media.ToMediaDto(result.ItemBookmarks);
        dto.UserState!.LastPlaybackPosition.Should().Be(333);
    }
}
