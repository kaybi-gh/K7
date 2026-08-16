using K7.Server.Application.Features.Medias.Commands.UpdateMediaMetadata;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class UpdateMediaMetadataCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaMetadataTagSyncService _tagSync = null!;
    private UpdateMediaMetadataCommandHandler _handler = null!;
    private Guid _serieId;
    private Guid _seasonId;
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

        _serieId = Guid.NewGuid();
        _seasonId = Guid.NewGuid();
        _episodeId = Guid.NewGuid();

        _context.Medias.Add(new Serie { Id = _serieId, Title = "Show" });
        _context.Medias.Add(new SerieSeason
        {
            Id = _seasonId,
            SerieId = _serieId,
            SeasonNumber = 1,
            Title = "S1"
        });
        _context.Medias.Add(new SerieEpisode
        {
            Id = _episodeId,
            SerieId = _serieId,
            SeasonId = _seasonId,
            EpisodeNumber = 1,
            Title = "Pilot",
            AirDate = new DateOnly(2020, 1, 1),
            ExternalIds =
            [
                new ExternalId { ProviderName = "imdb", Value = "tt1111111" },
                new ExternalId { ProviderName = "tmdb", Value = "42" }
            ]
        });
        _context.SaveChanges();

        _tagSync = Substitute.For<IMediaMetadataTagSyncService>();
        _handler = new UpdateMediaMetadataCommandHandler(_context, _tagSync);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReplaceImdbExternalId_WhenEpisodeAlreadyHasOne()
    {
        await _handler.Handle(new UpdateMediaMetadataCommand
        {
            Id = _episodeId,
            LockedFields = [],
            ExternalIds =
            [
                new ExternalIdEditDto { ProviderName = "imdb", Value = "tt9999999" },
                new ExternalIdEditDto { ProviderName = "tmdb", Value = "42" }
            ]
        }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.ExternalIds
            .Where(e => e.MediaId == _episodeId)
            .ToListAsync();

        saved.Should().HaveCount(2);
        saved.Should().ContainSingle(e => e.ProviderName == "imdb" && e.Value == "tt9999999");
        saved.Should().ContainSingle(e => e.ProviderName == "tmdb" && e.Value == "42");
        saved.Should().NotContain(e => e.Value == "tt1111111");
    }

    [Test]
    public async Task Handle_ShouldClearExternalIds_WhenEmptyListIsProvided()
    {
        await _handler.Handle(new UpdateMediaMetadataCommand
        {
            Id = _episodeId,
            LockedFields = [],
            ExternalIds = []
        }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.ExternalIds
            .Where(e => e.MediaId == _episodeId)
            .ToListAsync();

        saved.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldUpdateAirDate_WhenEpisodeAirDateIsProvided()
    {
        var airDate = new DateOnly(2024, 6, 15);

        await _handler.Handle(new UpdateMediaMetadataCommand
        {
            Id = _episodeId,
            LockedFields = [],
            AirDate = airDate
        }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var episode = await _context.Medias.OfType<SerieEpisode>()
            .SingleAsync(e => e.Id == _episodeId);

        episode.AirDate.Should().Be(airDate);
    }
}
