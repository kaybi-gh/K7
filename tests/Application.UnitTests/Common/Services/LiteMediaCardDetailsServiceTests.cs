using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Entities.Medias;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Common.Services;

[TestFixture]
public class LiteMediaCardDetailsServiceTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private LiteMediaCardDetailsService _sut = null!;

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
        _sut = new LiteMediaCardDetailsService(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ApplyAsync_ShouldAddGenreContentRatingAndRating_WhenTheMediaHasThem()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Dune", SortTitle = "Dune" };
        movie.MetadataTags.Add(Tag(movie, MetadataTagKind.Genre, "Science Fiction"));
        movie.MetadataTags.Add(Tag(movie, MetadataTagKind.ContentRating, "PG-13"));
        movie.Ratings.Add(new MetadataProviderRating
        {
            MetadataProvider = MetadataProvider.TMDb,
            Value = 8.04,
            MinimumValue = 0,
            MaximumValue = 10
        });
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();

        var result = await _sut.ApplyAsync([new LiteMovieDto { Id = movie.Id, Title = "Dune" }]);

        var details = result.Should().ContainSingle().Which.Details;
        details.Should().NotBeNull();
        details!.Genre.Should().Be("Science Fiction");
        details.ContentRating.Should().Be("PG-13");
        details.Rating.Should().Be(8.0);
    }

    [Test]
    public async Task ApplyAsync_ShouldScaleTheRatingToTen()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Dune", SortTitle = "Dune" };
        movie.Ratings.Add(new MetadataProviderRating
        {
            MetadataProvider = MetadataProvider.TMDb,
            Value = 4,
            MinimumValue = 0,
            MaximumValue = 5
        });
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();

        var result = await _sut.ApplyAsync([new LiteMovieDto { Id = movie.Id }]);

        result[0].Details!.Rating.Should().Be(8.0);
    }

    [Test]
    public async Task ApplyAsync_ShouldUseTheEpisodeRuntime()
    {
        var serie = new Serie { Id = Guid.NewGuid(), Title = "Show", SortTitle = "Show" };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };
        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Title = "Pilot",
            SortTitle = "Pilot",
            Runtime = 47
        };
        season.Episodes.Add(episode);
        serie.Seasons.Add(season);
        _context.Medias.AddRange(serie, season, episode);
        await _context.SaveChangesAsync();

        var result = await _sut.ApplyAsync(
        [
            new LiteSerieDto { Id = serie.Id },
            new LiteSerieEpisodeDto { Id = episode.Id, SerieId = serie.Id }
        ]);

        result[0].Details!.RuntimeMinutes.Should().Be(47);
        result[1].Details!.RuntimeMinutes.Should().Be(47);
    }

    [Test]
    public async Task ApplyAsync_ShouldLeaveTheItemAsIs_WhenTheMediaHasNoDetails()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Bare", SortTitle = "Bare" };
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();
        var item = new LiteMovieDto { Id = movie.Id, Title = "Bare" };

        var result = await _sut.ApplyAsync([item]);

        result.Should().ContainSingle().Which.Should().BeSameAs(item);
    }

    [Test]
    public async Task ApplyAsync_ShouldKeepTheOrderOfThePage()
    {
        var first = new Movie { Id = Guid.NewGuid(), Title = "A", SortTitle = "A" };
        var second = new Movie { Id = Guid.NewGuid(), Title = "B", SortTitle = "B" };
        second.MetadataTags.Add(Tag(second, MetadataTagKind.Genre, "Drama"));
        _context.Medias.AddRange(first, second);
        await _context.SaveChangesAsync();

        var result = await _sut.ApplyAsync(
        [
            new LiteMovieDto { Id = second.Id },
            new LiteMovieDto { Id = first.Id }
        ]);

        result.Select(r => r.Id).Should().Equal(second.Id, first.Id);
        result[0].Details!.Genre.Should().Be("Drama");
        result[1].Details.Should().BeNull();
    }

    private static MediaMetadataTag Tag(BaseMedia media, MetadataTagKind kind, string name) => new()
    {
        MediaId = media.Id,
        Media = media,
        MetadataTag = new MetadataTag
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            NormalizedKey = name.ToLowerInvariant(),
            DisplayName = name
        }
    };
}
