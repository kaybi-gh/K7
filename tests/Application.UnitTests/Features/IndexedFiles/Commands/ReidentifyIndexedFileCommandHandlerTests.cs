using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class ReidentifyIndexedFileCommandHandlerTests
{
    private const string RootPath = "/media/series";
    private const string DirectoryPath = "/media/series/Black Clover/Specials";

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private ReidentifyIndexedFileCommandHandler _handler = null!;

    private Guid _libraryId;

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

        var groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = RootPath,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        _handler = new ReidentifyIndexedFileCommandHandler(
            _context,
            _sender,
            availability,
            Substitute.For<IMusicIntelligenceCatalogReconciler>(),
            Substitute.For<ILogger<ReidentifyIndexedFileCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReparseFilenameAndAttachToCorrectEpisode_WhenStaleIdentificationExists()
    {
        var (serie, wrongEpisode, file) = await SeedSerieWithStaleSpecialIdentificationAsync();

        await _handler.Handle(new ReidentifyIndexedFileCommand
        {
            IndexedFileId = file.Id,
            SelectedProvider = "tmdb",
            SelectedExternalId = "tmdb-black-clover"
        }, CancellationToken.None);

        var attached = await _context.IndexedFiles.SingleAsync(f => f.Id == file.Id);
        var episode = await _context.Medias.OfType<SerieEpisode>()
            .Include(e => e.Season)
            .SingleAsync(e => e.Id == attached.MediaId);

        episode.SerieId.Should().Be(serie.Id);
        episode.Season.SeasonNumber.Should().Be(0);
        episode.EpisodeNumber.Should().Be(26);
        episode.Id.Should().NotBe(wrongEpisode.Id);
        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == wrongEpisode.Id)).Should().BeFalse();

        file = await _context.IndexedFiles.SingleAsync(f => f.Id == file.Id);
        file.Identification!.SeasonNumber.Should().Be(0);
        file.Identification.EpisodeNumber.Should().Be(26);
    }

    private async Task<(Serie Serie, SerieEpisode WrongEpisode, IndexedFile File)> SeedSerieWithStaleSpecialIdentificationAsync()
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

        // Path already renamed to E26, but Identification still says E99 (stale).
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = "Black Clover - S00E026.mkv",
            Extension = ".mkv",
            Path = $"{DirectoryPath}/Black Clover - S00E026.mkv",
            ParentDirectory = "Specials",
            Hash = 42,
            Size = 1,
            Identification = new MediaIdentification("Black Clover")
            {
                SeriesTitle = "Black Clover",
                SeasonNumber = 0,
                EpisodeNumber = 99
            }
        };

        var wrongEpisode = new SerieEpisode
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
        season.Episodes.Add(wrongEpisode);

        _context.Medias.Add(serie);
        _context.Medias.Add(season);
        _context.Medias.Add(wrongEpisode);
        _context.IndexedFiles.Add(file);
        await _context.SaveChangesAsync();
        return (serie, wrongEpisode, file);
    }
}
