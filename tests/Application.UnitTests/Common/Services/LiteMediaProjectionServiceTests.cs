using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Common.Services;

[TestFixture]
public class LiteMediaProjectionServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private LiteMediaProjectionService _sut = null!;
    private Guid _userId;

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
        _sut = new LiteMediaProjectionService(_context);
        _userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task GetLiteMediaDtosAsync_ShouldMarkSerieWatched_WhenAllEpisodesAreCompleted()
    {
        var (serieId, _, episodeIds) = SeedSerieWithEpisodes(episodeCount: 2);
        MarkCompleted(episodeIds[0]);
        MarkCompleted(episodeIds[1]);
        await _context.SaveChangesAsync();

        var dtos = await _sut.GetLiteMediaDtosAsync([serieId], _userId);

        var serie = dtos.Should().ContainSingle().Which.Should().BeOfType<LiteSerieDto>().Subject;
        serie.UserState.Should().NotBeNull();
        serie.UserState!.IsCompleted.Should().BeTrue();
        serie.UserState.ProgressPercentage.Should().Be(100);
    }

    [Test]
    public async Task GetLiteMediaDtosAsync_ShouldNotMarkSerieWatched_WhenSomeEpisodesRemain()
    {
        var (serieId, _, episodeIds) = SeedSerieWithEpisodes(episodeCount: 2);
        MarkCompleted(episodeIds[0]);
        await _context.SaveChangesAsync();

        var dtos = await _sut.GetLiteMediaDtosAsync([serieId], _userId);

        var serie = dtos.Should().ContainSingle().Which.Should().BeOfType<LiteSerieDto>().Subject;
        serie.UserState.Should().NotBeNull();
        serie.UserState!.IsCompleted.Should().BeFalse();
        serie.UserState.ProgressPercentage.Should().Be(50);
    }

    [Test]
    public async Task GetLiteMediaDtosAsync_ShouldLeaveSerieUserStateNull_WhenNoEpisodeHasWatchState()
    {
        var (serieId, _, _) = SeedSerieWithEpisodes(episodeCount: 2);
        await _context.SaveChangesAsync();

        var dtos = await _sut.GetLiteMediaDtosAsync([serieId], _userId);

        var serie = dtos.Should().ContainSingle().Which.Should().BeOfType<LiteSerieDto>().Subject;
        serie.UserState.Should().BeNull();
    }

    [Test]
    public async Task GetLiteMediaDtosAsync_ShouldMarkSeasonWatched_WhenAllEpisodesAreCompleted()
    {
        var (_, seasonId, episodeIds) = SeedSerieWithEpisodes(episodeCount: 2);
        MarkCompleted(episodeIds[0]);
        MarkCompleted(episodeIds[1]);
        await _context.SaveChangesAsync();

        var dtos = await _sut.GetLiteMediaDtosAsync([seasonId], _userId);

        var season = dtos.Should().ContainSingle().Which.Should().BeOfType<LiteSerieSeasonDto>().Subject;
        season.UserState.Should().NotBeNull();
        season.UserState!.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task GetLiteMediaDtosAsync_ShouldKeepMovieWatchState_WhenMovieIsCompleted()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Film", SortTitle = "Film" };
        _context.Medias.Add(movie);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = movie.Id,
            IsCompleted = true
        });
        await _context.SaveChangesAsync();

        var dtos = await _sut.GetLiteMediaDtosAsync([movie.Id], _userId);

        var dto = dtos.Should().ContainSingle().Which.Should().BeOfType<LiteMovieDto>().Subject;
        dto.UserState.Should().NotBeNull();
        dto.UserState!.IsCompleted.Should().BeTrue();
    }

    private (Guid SerieId, Guid SeasonId, List<Guid> EpisodeIds) SeedSerieWithEpisodes(int episodeCount)
    {
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
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

        var episodeIds = new List<Guid>(episodeCount);
        for (var i = 1; i <= episodeCount; i++)
        {
            var episodeId = Guid.NewGuid();
            var episode = new SerieEpisode
            {
                Id = episodeId,
                SerieId = serieId,
                Serie = serie,
                SeasonId = seasonId,
                Season = season,
                EpisodeNumber = i,
                Title = $"E{i}",
                SortTitle = $"E{i}"
            };
            season.Episodes.Add(episode);
            episodeIds.Add(episodeId);
        }

        _context.Medias.AddRange(serie, season);
        _context.Medias.AddRange(season.Episodes);
        return (serieId, seasonId, episodeIds);
    }

    private void MarkCompleted(Guid episodeId)
    {
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = episodeId,
            IsCompleted = true,
            LastInteractedAt = DateTime.UtcNow
        });
    }
}
