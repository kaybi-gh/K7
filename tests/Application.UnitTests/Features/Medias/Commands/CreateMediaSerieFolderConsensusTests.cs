using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
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
public class CreateMediaSerieFolderConsensusTests
{
    private const string RootPath = "/media/series";
    private const string DirectoryPath = "/media/series/Cool Show/Season 01";
    private const string ParentDirectoryName = "Season 01";
    private const string OtherSerieDirectoryPath = "/media/series/Other Show/Season 01";

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private CreateMediaCommandHandler _handler = null!;
    private ISerieMetadataProvider _serieProvider = null!;
    private CreateBackgroundTaskCommand? _capturedTask;

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

        _serieProvider = Substitute.For<ISerieMetadataProvider>();
        _serieProvider.ProviderName.Returns("tmdb");
        _serieProvider.SearchSerieAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("tmdb-wrong-show");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", _serieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();

        _sender = Substitute.For<ISender>();
        _capturedTask = null;
        _sender.Send(Arg.Do<CreateBackgroundTaskCommand>(c => _capturedTask = c), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        var serieIdentity = new SerieMetadataIdentityService(
            Enumerable.Empty<ISearchableMetadataProvider>(),
            _serviceProviderRoot,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

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
            serieIdentity,
            new MusicMetadataIdentityService(_serviceProviderRoot, Substitute.For<ILogger<MusicMetadataIdentityService>>()),
            Substitute.For<IMusicIntelligenceCatalogReconciler>(),
            new PlaybackBookmarkService(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackBookmarkService>.Instance),
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
    public async Task Handle_ShouldAttachToFolderSerie_WhenSiblingAlreadyMatched()
    {
        var existingSerie = await SeedSerieWithEpisodeAsync(
            "Cool Show",
            DirectoryPath,
            season: 1,
            episode: 1,
            externalId: "tmdb-cool-show");
        var newFile = await SeedSerieIndexedFileAsync(
            DirectoryPath,
            "Cool Show - S01E02.mkv",
            seriesTitle: "Totally Wrong Title",
            season: 1,
            episode: 2);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [newFile.Id]
        }, CancellationToken.None);

        mediaId.Should().Be(existingSerie.Id);
        await _serieProvider.DidNotReceive()
            .SearchSerieAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var attached = await _context.IndexedFiles.SingleAsync(f => f.Id == newFile.Id);
        var episode = await _context.Medias.OfType<SerieEpisode>()
            .Include(e => e.Season)
            .SingleAsync(e => e.Id == attached.MediaId);
        episode.SerieId.Should().Be(existingSerie.Id);
        episode.Season.SeasonNumber.Should().Be(1);
        episode.EpisodeNumber.Should().Be(2);

        var refresh = CaptureRefreshCommand();
        refresh.Should().NotBeNull();
        refresh!.Incremental.Should().BeTrue();
        refresh.MediaId.Should().Be(existingSerie.Id);
    }

    [Test]
    public async Task Handle_ShouldNotAttachToOtherSerie_WhenParentDirectoryNameCollides()
    {
        var existingSerie = await SeedSerieWithEpisodeAsync(
            "Cool Show",
            DirectoryPath,
            season: 1,
            episode: 1,
            externalId: "tmdb-cool-show");
        var newFile = await SeedSerieIndexedFileAsync(
            OtherSerieDirectoryPath,
            "Other Show - S01E01.mkv",
            seriesTitle: "Other Show",
            season: 1,
            episode: 1);

        _serieProvider.SearchSerieAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("tmdb-other-show");

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [newFile.Id]
        }, CancellationToken.None);

        mediaId.Should().NotBe(existingSerie.Id);
        await _serieProvider.Received(1)
            .SearchSerieAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var series = await _context.Medias.OfType<Serie>().ToListAsync();
        series.Should().HaveCount(2);
        series.Should().Contain(s => s.Id == existingSerie.Id && s.Title == "Cool Show");
        series.Should().Contain(s => s.Id == mediaId && s.Title == "Other Show");
    }

    [Test]
    public async Task Handle_ShouldSearchProvider_WhenFolderHasNoSiblingSeries()
    {
        var newFile = await SeedSerieIndexedFileAsync(
            DirectoryPath,
            "Brand New Show - S01E01.mkv",
            seriesTitle: "Brand New Show",
            season: 1,
            episode: 1);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [newFile.Id]
        }, CancellationToken.None);

        await _serieProvider.Received(1)
            .SearchSerieAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var serie = await _context.Medias.OfType<Serie>().SingleAsync(s => s.Id == mediaId);
        serie.ExternalIds.Should().ContainSingle(e => e.Value == "tmdb-wrong-show");

        var refresh = CaptureRefreshCommand();
        refresh.Should().NotBeNull();
        refresh!.Incremental.Should().BeFalse();
        refresh.MediaId.Should().Be(mediaId);
    }

    [Test]
    public async Task Handle_ShouldUnifyCloseTitles_BeforeCreatingSerie()
    {
        var file1 = await SeedSerieIndexedFileAsync(
            DirectoryPath,
            "Show Name - S01E01.mkv",
            seriesTitle: "Show Name",
            season: 1,
            episode: 1);
        var file2 = await SeedSerieIndexedFileAsync(
            DirectoryPath,
            "Show Nam - S01E02.mkv",
            seriesTitle: "Show Nam",
            season: 1,
            episode: 2);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [file1.Id, file2.Id]
        }, CancellationToken.None);

        (await _context.Medias.OfType<Serie>().CountAsync()).Should().Be(1);
        var serie = await _context.Medias.OfType<Serie>().SingleAsync(s => s.Id == mediaId);
        serie.Title.Should().Be("Show Name");

        var episodes = await _context.Medias.OfType<SerieEpisode>()
            .Where(e => e.SerieId == mediaId)
            .ToListAsync();
        episodes.Should().HaveCount(2);
    }

    private RefreshMediaMetadatasCommand? CaptureRefreshCommand()
        => _capturedTask?.Request as RefreshMediaMetadatasCommand;

    private async Task<Serie> SeedSerieWithEpisodeAsync(
        string title,
        string directoryPath,
        int season,
        int episode,
        string externalId)
    {
        var serie = new Serie
        {
            Id = Guid.NewGuid(),
            Title = title,
            SortTitle = title
        };
        serie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = externalId });

        var seasonEntity = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = season,
            Title = $"Season {season}",
            SortTitle = $"Season {season}"
        };
        serie.Seasons.Add(seasonEntity);

        var episodeFile = await SeedSerieIndexedFileAsync(
            directoryPath,
            $"{title} - S{season:00}E{episode:00}.mkv",
            title,
            season,
            episode,
            persist: false);

        var episodeEntity = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = seasonEntity.Id,
            Season = seasonEntity,
            EpisodeNumber = episode,
            Title = $"Episode {episode}",
            SortTitle = $"Episode {episode}",
            IndexedFiles = [episodeFile]
        };
        seasonEntity.Episodes.Add(episodeEntity);

        _context.Medias.Add(serie);
        _context.Medias.Add(seasonEntity);
        _context.Medias.Add(episodeEntity);
        _context.IndexedFiles.Add(episodeFile);
        await _context.SaveChangesAsync();
        return serie;
    }

    private async Task<IndexedFile> SeedSerieIndexedFileAsync(
        string directoryPath,
        string fileName,
        string seriesTitle,
        int season,
        int episode,
        bool persist = true)
    {
        var indexedFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = fileName,
            Extension = ".mkv",
            Path = $"{directoryPath}/{fileName}",
            // Production stores only the folder name, not the full path.
            ParentDirectory = ParentDirectoryName,
            Hash = (uint)Random.Shared.Next(1, int.MaxValue),
            Size = 1,
            Identification = new MediaIdentification(seriesTitle)
            {
                SeriesTitle = seriesTitle,
                SeasonNumber = season,
                EpisodeNumber = episode
            }
        };

        if (persist)
        {
            _context.IndexedFiles.Add(indexedFile);
            await _context.SaveChangesAsync();
        }

        return indexedFile;
    }
}
