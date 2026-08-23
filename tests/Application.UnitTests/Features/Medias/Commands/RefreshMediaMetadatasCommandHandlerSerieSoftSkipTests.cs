using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class RefreshMediaMetadatasCommandHandlerSerieSoftSkipTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private ISerieMetadataProvider _serieProvider = null!;
    private RefreshMediaMetadatasCommandHandler _handler = null!;

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

        _serieProvider = Substitute.For<ISerieMetadataProvider>();
        _serieProvider.ProviderName.Returns("tvdb");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tvdb", _serieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _handler = new RefreshMediaMetadatasCommandHandler(
            _context,
            _serviceProviderRoot,
            Substitute.For<ISender>(),
            [],
            Substitute.For<IMediaMetadataTagSyncService>(),
            new MetadataPictureDeletionService(
                _context,
                Substitute.For<ILogger<MetadataPictureDeletionService>>()),
            Substitute.For<ILogger<RefreshMediaMetadatasCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProviderRoot.Dispose();
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldContinue_WhenOneEpisodeIsMissingFromProvider()
    {
        var serie = new Serie { Title = "Fate/EXTRA Last Encore" };
        var season = new SerieSeason
        {
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1"
        };
        var foundEpisode = new SerieEpisode
        {
            Serie = serie,
            Season = season,
            EpisodeNumber = 1,
            Title = "Local Episode 1"
        };
        var missingEpisode = new SerieEpisode
        {
            Serie = serie,
            Season = season,
            EpisodeNumber = 99,
            Title = "Local Missing Episode"
        };
        season.Episodes.Add(foundEpisode);
        season.Episodes.Add(missingEpisode);
        serie.Seasons.Add(season);
        _context.Medias.Add(serie);
        await _context.SaveChangesAsync();

        _serieProvider.FetchSerieMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalSerieMetadata { Title = "Fate/EXTRA Last Encore" });

        _serieProvider.FetchSeasonMetadataAsync(
                Arg.Any<string>(),
                1,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalSeasonMetadata { SeasonNumber = 1, Title = "Season 1" });

        _serieProvider.FetchEpisodeMetadataAsync(
                Arg.Any<string>(),
                1,
                1,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalEpisodeMetadata
            {
                SeasonNumber = 1,
                EpisodeNumber = 1,
                Title = "Refreshed Episode 1"
            });

        _serieProvider.FetchEpisodeMetadataAsync(
                Arg.Any<string>(),
                1,
                99,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(_ => Task.FromException<ExternalEpisodeMetadata>(
                new InvalidOperationException("TVDB episode S1E99 not found for series 337018.")));

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = serie.Id,
            MetadataProviderExternalId = "337018",
            MetadataProviderName = "tvdb",
            Language = "fr",
            FallbackLanguage = "en"
        }, CancellationToken.None);

        var refreshed = await _context.Medias.OfType<Serie>()
            .Include(s => s.Seasons)
            .ThenInclude(s => s.Episodes)
            .SingleAsync(s => s.Id == serie.Id);

        refreshed.Title.Should().Be("Fate/EXTRA Last Encore");
        refreshed.LastMetadataRefreshedAt.Should().NotBeNull();

        var episodes = refreshed.Seasons.Single().Episodes.OrderBy(e => e.EpisodeNumber).ToList();
        episodes.Should().HaveCount(2);
        episodes[0].Title.Should().Be("Refreshed Episode 1");
        episodes[1].Title.Should().Be("Local Missing Episode");
    }

    [Test]
    public async Task Handle_ShouldResolveTvdbProvider_WhenRequestedProviderIsAuto()
    {
        var serie = new Serie
        {
            Title = "Cool Show",
            NumberingProviderName = "tvdb"
        };
        serie.ExternalIds.Add(new ExternalId { ProviderName = "tvdb", Value = "337018" });
        _context.Medias.Add(serie);
        await _context.SaveChangesAsync();

        _serieProvider.FetchSerieMetadataAsync(
                "337018",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalSerieMetadata { Title = "Cool Show", Status = "Ended" });

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = serie.Id,
            MetadataProviderExternalId = "337018",
            MetadataProviderName = "auto",
            Language = "fr",
            FallbackLanguage = "en"
        }, CancellationToken.None);

        var refreshed = await _context.Medias.OfType<Serie>()
            .SingleAsync(s => s.Id == serie.Id);

        refreshed.Status.Should().Be("Ended");
        refreshed.LastMetadataRefreshedAt.Should().NotBeNull();
        refreshed.NumberingProviderName.Should().Be("tvdb");

        await _serieProvider.Received(1).FetchSerieMetadataAsync(
            "337018",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
    }
}
