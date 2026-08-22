using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.EventHandlers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.EventHandlers;

[TestFixture]
public class IndexedFileDeletedEventHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IndexedFileDeletedEventHandler _handler = null!;

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

        _handler = new IndexedFileDeletedEventHandler(
            _context,
            Substitute.For<IMusicIntelligenceCatalogReconciler>(),
            Substitute.For<IMediaQueryCacheInvalidator>(),
            NullLogger<IndexedFileDeletedEventHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldDeleteOrphanEpisodeSeasonAndSerie_WhenLastIndexedFileRemoved()
    {
        var (episodeId, seasonId, serieId, indexedFile) = await SeedEpisodeWithFileAsync();

        _context.IndexedFiles.Remove(indexedFile);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new IndexedFileDeletedEvent(indexedFile, episodeId, indexedFile.LibraryId),
            CancellationToken.None);
        await _context.SaveChangesAsync();

        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeFalse();
        (await _context.Medias.OfType<SerieSeason>().AnyAsync(s => s.Id == seasonId)).Should().BeFalse();
        (await _context.Medias.OfType<Serie>().AnyAsync(s => s.Id == serieId)).Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldKeepEpisode_WhenAnotherIndexedFileRemains()
    {
        var (episodeId, seasonId, serieId, indexedFile) = await SeedEpisodeWithFileAsync();
        var secondFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = indexedFile.LibraryId,
            Name = "Show - S01E01 - alt.mkv",
            Extension = ".mkv",
            Path = "/media/series/Show/Season 1/Show - S01E01 - alt.mkv",
            ParentDirectory = "Season 1",
            Hash = 2,
            Size = 2,
            MediaId = episodeId
        };
        _context.IndexedFiles.Add(secondFile);
        await _context.SaveChangesAsync();

        _context.IndexedFiles.Remove(indexedFile);
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new IndexedFileDeletedEvent(indexedFile, episodeId, indexedFile.LibraryId),
            CancellationToken.None);
        await _context.SaveChangesAsync();

        (await _context.Medias.OfType<SerieEpisode>().AnyAsync(e => e.Id == episodeId)).Should().BeTrue();
        (await _context.Medias.OfType<SerieSeason>().AnyAsync(s => s.Id == seasonId)).Should().BeTrue();
        (await _context.Medias.OfType<Serie>().AnyAsync(s => s.Id == serieId)).Should().BeTrue();
    }

    private async Task<(Guid EpisodeId, Guid SeasonId, Guid SerieId, IndexedFile IndexedFile)> SeedEpisodeWithFileAsync()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = "/media/series",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

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
        serie.Seasons.Add(season);

        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonId = season.Id,
            Season = season,
            EpisodeNumber = 1,
            Title = "Episode 1",
            SortTitle = "Episode 1",
            IndexedFiles = []
        };
        season.Episodes.Add(episode);

        var indexedFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            Name = "Show - S01E01.mkv",
            Extension = ".mkv",
            Path = "/media/series/Show/Season 1/Show - S01E01.mkv",
            ParentDirectory = "Season 1",
            Hash = 1,
            Size = 1,
            MediaId = episode.Id
        };
        episode.IndexedFiles.Add(indexedFile);

        _context.Medias.AddRange(serie, season, episode);
        _context.IndexedFiles.Add(indexedFile);
        await _context.SaveChangesAsync();

        return (episode.Id, season.Id, serie.Id, indexedFile);
    }
}
