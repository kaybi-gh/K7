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
public class CreateMediaSerieTitleYearTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private CreateMediaCommandHandler _handler = null!;
    private ISerieMetadataProvider _serieProvider = null!;

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
            RootPath = "/media/series",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        _serieProvider = Substitute.For<ISerieMetadataProvider>();
        _serieProvider.ProviderName.Returns("tmdb");
        _serieProvider.SearchSerieAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var identification = callInfo.ArgAt<MediaIdentification>(0);
                return identification.ReleaseYear?.Year switch
                {
                    1999 => "tmdb-one-piece-anime",
                    2023 => "tmdb-one-piece-live",
                    _ => null
                };
            });

        var tvdbProvider = Substitute.For<ISerieMetadataProvider>();
        tvdbProvider.ProviderName.Returns("tvdb");
        tvdbProvider.SearchSerieAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", _serieProvider);
        services.AddKeyedSingleton("tvdb", tvdbProvider);
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
    public async Task Handle_ShouldCreateSeparateSeries_WhenSameTitleDifferentYears()
    {
        var animeFile = await SeedEpisodeFileAsync(
            "One Piece (1999)/Season 01/One Piece - S01E01.mkv",
            "One Piece",
            new DateOnly(1999, 1, 1),
            season: 1,
            episode: 1);
        var liveFile = await SeedEpisodeFileAsync(
            "One Piece (2023)/Season 01/One Piece - S01E01.mkv",
            "One Piece",
            new DateOnly(2023, 1, 1),
            season: 1,
            episode: 1);

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [animeFile.Id]
        }, CancellationToken.None);

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [liveFile.Id]
        }, CancellationToken.None);

        var series = await _context.Medias.OfType<Serie>()
            .Include(s => s.ExternalIds)
            .OrderBy(s => s.ReleaseDate)
            .ToListAsync();

        series.Should().HaveCount(2);
        series[0].Title.Should().Be("One Piece");
        series[0].ReleaseDate.Should().Be(new DateOnly(1999, 1, 1));
        series[0].ExternalIds.Should().ContainSingle(e => e.Value == "tmdb-one-piece-anime");
        series[1].Title.Should().Be("One Piece");
        series[1].ReleaseDate.Should().Be(new DateOnly(2023, 1, 1));
        series[1].ExternalIds.Should().ContainSingle(e => e.Value == "tmdb-one-piece-live");

        var animeAttached = await _context.IndexedFiles.SingleAsync(f => f.Id == animeFile.Id);
        var liveAttached = await _context.IndexedFiles.SingleAsync(f => f.Id == liveFile.Id);
        var animeEpisode = await _context.Medias.OfType<SerieEpisode>()
            .SingleAsync(e => e.Id == animeAttached.MediaId);
        var liveEpisode = await _context.Medias.OfType<SerieEpisode>()
            .SingleAsync(e => e.Id == liveAttached.MediaId);

        animeEpisode.SerieId.Should().Be(series[0].Id);
        liveEpisode.SerieId.Should().Be(series[1].Id);
        animeEpisode.SerieId.Should().NotBe(liveEpisode.SerieId);
    }

    [Test]
    public async Task Handle_ShouldCreateSeparateSeries_WhenProviderMissesButYearsDiffer()
    {
        _serieProvider.SearchSerieAsync(
                Arg.Any<MediaIdentification>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var animeFile = await SeedEpisodeFileAsync(
            "One Piece (1999)/Season 01/One Piece - S01E01.mkv",
            "One Piece",
            new DateOnly(1999, 1, 1),
            season: 1,
            episode: 1);
        var liveFile = await SeedEpisodeFileAsync(
            "One Piece (2023)/Season 01/One Piece - S01E01.mkv",
            "One Piece",
            new DateOnly(2023, 1, 1),
            season: 1,
            episode: 1);

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [animeFile.Id]
        }, CancellationToken.None);

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Serie,
            LibraryId = _libraryId,
            IndexedFileIds = [liveFile.Id]
        }, CancellationToken.None);

        var series = await _context.Medias.OfType<Serie>().OrderBy(s => s.ReleaseDate).ToListAsync();
        series.Should().HaveCount(2);
        series[0].ReleaseDate.Should().Be(new DateOnly(1999, 1, 1));
        series[1].ReleaseDate.Should().Be(new DateOnly(2023, 1, 1));
    }

    private async Task<IndexedFile> SeedEpisodeFileAsync(
        string relativePath,
        string seriesTitle,
        DateOnly releaseYear,
        int season,
        int episode)
    {
        var parentDirectory = Path.GetFileName(Path.GetDirectoryName(relativePath)!)!;
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = Path.GetFileName(relativePath),
            Extension = Path.GetExtension(relativePath),
            Path = $"/media/series/{relativePath}",
            ParentDirectory = parentDirectory,
            Hash = (uint)Random.Shared.Next(),
            Size = 1,
            Identification = new MediaIdentification(seriesTitle)
            {
                SeriesTitle = seriesTitle,
                ReleaseYear = releaseYear,
                SeasonNumber = season,
                EpisodeNumber = episode
            }
        };

        _context.IndexedFiles.Add(file);
        await _context.SaveChangesAsync();
        return file;
    }
}
