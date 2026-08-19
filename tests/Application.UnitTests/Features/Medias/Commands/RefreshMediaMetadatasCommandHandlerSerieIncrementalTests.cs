using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Features.MetadataPictures.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class RefreshMediaMetadatasCommandHandlerSerieIncrementalTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private ISerieMetadataProvider _serieProvider = null!;
    private ISender _sender = null!;
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
        _serieProvider.ProviderName.Returns("tmdb");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", _serieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();
        _sender = Substitute.For<ISender>();

        _handler = new RefreshMediaMetadatasCommandHandler(
            _context,
            _serviceProviderRoot,
            _sender,
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
    public async Task Handle_ShouldRefreshOnlyUnenrichedEpisode_WhenIncremental()
    {
        var (serie, existingStillId) = await SeedSerieWithEnrichedAndNewEpisodeAsync();

        _serieProvider.FetchSerieMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalSerieMetadata
            {
                Title = "Cool Show",
                Status = "Ended",
                Pictures =
                [
                    new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        OriginalRemoteUri = new Uri("https://img.example/new-poster.jpg")
                    }
                ]
            });

        _serieProvider.FetchSeasonMetadataAsync(
                Arg.Any<string>(),
                1,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalSeasonMetadata
            {
                SeasonNumber = 1,
                Title = "Season 1",
                Overview = "Updated season overview",
                Pictures =
                [
                    new MetadataPicture
                    {
                        Type = MetadataPictureType.Poster,
                        OriginalRemoteUri = new Uri("https://img.example/new-season.jpg")
                    }
                ]
            });

        _serieProvider.TryBuildEpisodeMetadataFromCatalogAsync(
                Arg.Any<string>(),
                1,
                2,
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ExternalEpisodeMetadata
            {
                SeasonNumber = 1,
                EpisodeNumber = 2,
                Title = "The Follow-up",
                Overview = "New episode overview"
            });

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = serie.Id,
            MetadataProviderExternalId = "tmdb-1",
            MetadataProviderName = "tmdb",
            Language = "fr",
            FallbackLanguage = "en",
            Incremental = true
        }, CancellationToken.None);

        var refreshed = await _context.Medias.OfType<Serie>()
            .Include(s => s.Pictures)
            .Include(s => s.Seasons)
                .ThenInclude(s => s.Pictures)
            .Include(s => s.Seasons)
                .ThenInclude(s => s.Episodes)
                    .ThenInclude(e => e.Pictures)
            .SingleAsync(s => s.Id == serie.Id);

        refreshed.Status.Should().Be("Ended");
        refreshed.Pictures.Should().ContainSingle(p => p.Type == MetadataPictureType.Poster);
        refreshed.Pictures.Single(p => p.Type == MetadataPictureType.Poster)
            .OriginalRemoteUri.Should().Be(new Uri("https://img.example/existing-poster.jpg"));

        var season1 = refreshed.Seasons.Single(s => s.SeasonNumber == 1);
        season1.Pictures.Should().ContainSingle(p => p.Type == MetadataPictureType.Poster);
        season1.Pictures.Single().OriginalRemoteUri.Should().Be(new Uri("https://img.example/existing-season.jpg"));

        var episodes = season1.Episodes.OrderBy(e => e.EpisodeNumber).ToList();
        episodes[0].Title.Should().Be("Pilot");
        episodes[0].Pictures.Should().ContainSingle(p => p.Id == existingStillId);
        episodes[1].Title.Should().Be("The Follow-up");
        episodes[1].Overview.Should().Be("New episode overview");

        await _serieProvider.DidNotReceive().FetchSeasonMetadataAsync(
            Arg.Any<string>(),
            2,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
        await _serieProvider.DidNotReceive().TryBuildEpisodeMetadataFromCatalogAsync(
            Arg.Any<string>(),
            1,
            1,
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await _serieProvider.DidNotReceive().FetchEpisodeMetadataAsync(
            Arg.Any<string>(),
            1,
            1,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
    }

    [Test]
    public async Task Handle_ShouldRefreshAllEpisodes_WhenNotIncremental()
    {
        var (serie, _) = await SeedSerieWithEnrichedAndNewEpisodeAsync();

        _serieProvider.FetchSerieMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(new ExternalSerieMetadata { Title = "Cool Show", Status = "Returning Series" });

        _serieProvider.FetchSeasonMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(call => new ExternalSeasonMetadata
            {
                SeasonNumber = call.ArgAt<int>(1),
                Title = $"Season {call.ArgAt<int>(1)}"
            });

        _serieProvider.TryBuildEpisodeMetadataFromCatalogAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ExternalEpisodeMetadata
            {
                SeasonNumber = call.ArgAt<int>(1),
                EpisodeNumber = call.ArgAt<int>(2),
                Title = $"Refreshed S{call.ArgAt<int>(1)}E{call.ArgAt<int>(2)}"
            });

        await _handler.Handle(new RefreshMediaMetadatasCommand
        {
            MediaId = serie.Id,
            MetadataProviderExternalId = "tmdb-1",
            MetadataProviderName = "tmdb",
            Language = "fr",
            FallbackLanguage = "en"
        }, CancellationToken.None);

        var refreshed = await _context.Medias.OfType<Serie>()
            .Include(s => s.Seasons)
                .ThenInclude(s => s.Episodes)
            .SingleAsync(s => s.Id == serie.Id);

        var season1 = refreshed.Seasons.Single(s => s.SeasonNumber == 1);
        season1.Episodes.Single(e => e.EpisodeNumber == 1).Title.Should().Be("Refreshed S1E1");
        season1.Episodes.Single(e => e.EpisodeNumber == 2).Title.Should().Be("Refreshed S1E2");
        refreshed.Seasons.Single(s => s.SeasonNumber == 2)
            .Episodes.Single().Title.Should().Be("Refreshed S2E1");

        await _serieProvider.Received(1).FetchSeasonMetadataAsync(
            Arg.Any<string>(),
            2,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
    }

    private async Task<(Serie Serie, Guid ExistingStillId)> SeedSerieWithEnrichedAndNewEpisodeAsync()
    {
        var serie = new Serie { Title = "Cool Show", Status = "Returning Series" };
        serie.Pictures.Add(new MetadataPicture
        {
            Type = MetadataPictureType.Poster,
            OriginalRemoteUri = new Uri("https://img.example/existing-poster.jpg")
        });

        var season1 = new SerieSeason
        {
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            Overview = "First season"
        };
        season1.Pictures.Add(new MetadataPicture
        {
            Type = MetadataPictureType.Poster,
            OriginalRemoteUri = new Uri("https://img.example/existing-season.jpg")
        });

        var existingStill = new MetadataPicture
        {
            Type = MetadataPictureType.Still,
            OriginalRemoteUri = new Uri("https://img.example/pilot-still.jpg")
        };
        var enriched = new SerieEpisode
        {
            Serie = serie,
            Season = season1,
            EpisodeNumber = 1,
            Title = "Pilot",
            Overview = "The start"
        };
        enriched.Pictures.Add(existingStill);
        enriched.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "ep-1" });

        var unenriched = new SerieEpisode
        {
            Serie = serie,
            Season = season1,
            EpisodeNumber = 2,
            Title = "Episode 2"
        };

        season1.Episodes.Add(enriched);
        season1.Episodes.Add(unenriched);

        var season2 = new SerieSeason
        {
            Serie = serie,
            SeasonNumber = 2,
            Title = "Season 2",
            Overview = "Second season"
        };
        var season2Episode = new SerieEpisode
        {
            Serie = serie,
            Season = season2,
            EpisodeNumber = 1,
            Title = "Season two premiere",
            Overview = "Already enriched"
        };
        season2.Episodes.Add(season2Episode);

        serie.Seasons.Add(season1);
        serie.Seasons.Add(season2);
        _context.Medias.Add(serie);
        await _context.SaveChangesAsync();

        return (serie, existingStill.Id);
    }
}
