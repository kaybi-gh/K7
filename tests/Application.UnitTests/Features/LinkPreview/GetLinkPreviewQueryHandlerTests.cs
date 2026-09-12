using K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.LinkPreview;

[TestFixture]
public class GetLinkPreviewQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetLinkPreviewQueryHandler _handler = null!;

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
        _handler = new GetLinkPreviewQueryHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnNull_WhenPathIsNotAMediaPage()
    {
        var result = await _handler.Handle(new GetLinkPreviewQuery("/"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldReturnTitleOverviewAndPoster_WhenMovieExists()
    {
        var movieId = Guid.NewGuid();
        var posterId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = movieId,
            Title = "Sintel",
            Overview = "A girl and a dragon."
        };
        movie.Pictures.Add(new MetadataPicture
        {
            Id = posterId,
            Type = MetadataPictureType.Poster,
            LocalPath = "/tmp/sintel.jpg"
        });
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetLinkPreviewQuery($"/movies/{movieId}"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Sintel");
        result.Description.Should().Be("A girl and a dragon.");
        result.PictureId.Should().Be(posterId);
    }

    [Test]
    public async Task Handle_ShouldFallBackToSeriePoster_WhenSeasonHasNone()
    {
        var serieId = Guid.NewGuid();
        var posterId = Guid.NewGuid();
        var serie = new Serie { Id = serieId, Title = "Show", Overview = "A show." };
        serie.Pictures.Add(new MetadataPicture
        {
            Id = posterId,
            Type = MetadataPictureType.Poster,
            LocalPath = "/tmp/show.jpg"
        });
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1"
        };
        serie.Seasons.Add(season);
        _context.Medias.AddRange(serie, season);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetLinkPreviewQuery($"/series/{serieId}/seasons/1"),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Show: Season 1");
        result.PictureId.Should().Be(posterId);
    }
}
