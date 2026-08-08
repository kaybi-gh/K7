using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class CreateMediaSerieRelinkTests
{
    private const string RootPath = "/media/series";
    private const string DirectoryPath = "/media/series/Black Clover/Specials";
    private const string ParentDirectoryName = "Specials";

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private CreateMediaCommandHandler _handler = null!;

    private Guid _libraryId;
    private Guid _groupId;

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

        _groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = _groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = RootPath,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        var serieProvider = Substitute.For<ISerieMetadataProvider>();
        serieProvider.ProviderName.Returns("tmdb");
        serieProvider.SearchSerieAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns("tmdb-black-clover");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", serieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        _handler = new CreateMediaCommandHandler(
            _context,
            _sender,
            _serviceProviderRoot,
            Substitute.For<IAudioTagReader>(),
            Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() }),
            Substitute.For<IMediaMetadataTagSyncService>(),
            new MediaIdentityLookupService(_context),
            new MediaIdentityLock(),
            availability,
            Substitute.For<ILogger<CreateMediaCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
        _serviceProviderRoot.Dispose();
    }

    [Test]
    public async Task Handle_ShouldRelinkFileAndDeleteOrphan_WhenEpisodeIdentityChanges()
    {
        var (serie, oldEpisode, file) = await SeedSerieWithWrongSpecialAsync();

        file.Name = "Black Clover - S00E026.mkv";
        file.Path = $"{DirectoryPath}/{file.Name}";
        file.Identification = new MediaIdentification("Black Clover")
        {
            SeriesTitle = "Black Clover",
            SeasonNumber = 0,
            EpisodeNumber = 26
        };
        await _context.SaveChangesAsync();

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [file.Id]
        }, CancellationToken.None);

        var attached = await _context.IndexedFiles.SingleAsync(f => f.Id == file.Id);
        var newEpisode = await _context.Medias.OfType<SerieEpisode>()
            .Include(e => e.Season)
            .SingleAsync(e => e.Id == attached.MediaId);

        newEpisode.SerieId.Should().Be(serie.Id);
        newEpisode.Season.SeasonNumber.Should().Be(0);
        newEpisode.EpisodeNumber.Should().Be(26);
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == oldEpisode.Id)).Should().BeFalse();
    }

    private async Task<(Serie Serie, SerieEpisode Episode, IndexedFile File)> SeedSerieWithWrongSpecialAsync()
    {
        var serie = new Serie
        {
            Id = Guid.NewGuid(),
            Title = "Black Clover",
            SortTitle = "Black Clover"
        };
        serie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "tmdb-black-clover" });

        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 0,
            Title = "Specials",
            SortTitle = "Specials"
        };
        serie.Seasons.Add(season);

        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = "Black Clover - S00E099.mkv",
            Extension = ".mkv",
            Path = $"{DirectoryPath}/Black Clover - S00E099.mkv",
            ParentDirectory = ParentDirectoryName,
            Hash = 42,
            Size = 1,
            Identification = new MediaIdentification("Black Clover")
            {
                SeriesTitle = "Black Clover",
                SeasonNumber = 0,
                EpisodeNumber = 99
            }
        };

        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 99,
            Title = "Episode 99",
            SortTitle = "Episode 99",
            IndexedFiles = [file]
        };
        season.Episodes.Add(episode);

        _context.Medias.Add(serie);
        _context.Medias.Add(season);
        _context.Medias.Add(episode);
        _context.IndexedFiles.Add(file);
        await _context.SaveChangesAsync();
        return (serie, episode, file);
    }
}
