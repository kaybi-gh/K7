using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticsSummary;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.Diagnostics.Queries;

[TestFixture]
public class GetDiagnosticsSummaryQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetDiagnosticsSummaryQueryHandler _handler = null!;
    private PathsConfiguration _paths = null!;

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

        _paths = new PathsConfiguration { Metadatas = Path.Combine(Path.GetTempPath(), "k7-diag-summary-tests") };
        _handler = new GetDiagnosticsSummaryQueryHandler(_context, Options.Create(_paths));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCountMediaWithoutFiles_FromAvailabilityLeafTypes()
    {
        var libraryId = SeedMovieLibrary();
        var withFileId = Guid.NewGuid();
        var withoutFileId = Guid.NewGuid();

        _context.Medias.AddRange(
            new Movie { Id = withFileId, Title = "Has File" },
            new Movie { Id = withoutFileId, Title = "Missing File" });

        _context.MediaLibraryAvailabilities.AddRange(
            new MediaLibraryAvailability { LibraryId = libraryId, MediaId = withFileId },
            new MediaLibraryAvailability { LibraryId = libraryId, MediaId = withoutFileId });

        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            MediaId = withFileId,
            Name = "movie",
            Extension = ".mkv",
            Path = "/media/movie.mkv",
            ParentDirectory = "/media",
            Hash = 1u,
            Size = 1
        });

        await _context.SaveChangesAsync();

        var summaries = await _handler.Handle(new GetDiagnosticsSummaryQuery(), CancellationToken.None);

        var summary = summaries.Should().ContainSingle().Subject;
        summary.LibraryId.Should().Be(libraryId);
        summary.MediaWithoutFilesCount.Should().Be(1);
        summary.TotalMediaCount.Should().Be(2);
    }

    [Test]
    public async Task Handle_ShouldCountMergedOrphansAndIdentifiedOrphansSeparately()
    {
        var libraryId = SeedMovieLibrary();
        var linkedMediaId = Guid.NewGuid();

        _context.Medias.Add(new Movie { Id = linkedMediaId, Title = "Linked Unidentified" });
        _context.IndexedFiles.AddRange(
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = libraryId,
                Name = "identified-orphan",
                Extension = ".mkv",
                Path = "/media/identified-orphan.mkv",
                ParentDirectory = "/media",
                Hash = 1u,
                Size = 1,
                MediaId = null,
                Identification = new MediaIdentification("Known") { ReleaseYear = new DateOnly(2020, 1, 1) }
            },
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = libraryId,
                Name = "unidentified-orphan",
                Extension = ".mkv",
                Path = "/media/unidentified-orphan.mkv",
                ParentDirectory = "/media",
                Hash = 2u,
                Size = 1,
                MediaId = null,
                Identification = null
            },
            new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = libraryId,
                Name = "linked-unidentified",
                Extension = ".mkv",
                Path = "/media/linked-unidentified.mkv",
                ParentDirectory = "/media",
                Hash = 3u,
                Size = 1,
                MediaId = linkedMediaId,
                Identification = null
            });
        await _context.SaveChangesAsync();

        var summaries = await _handler.Handle(new GetDiagnosticsSummaryQuery(), CancellationToken.None);
        var summary = summaries.Should().ContainSingle(s => s.LibraryId == libraryId).Subject;

        summary.IdentifiedOrphanIndexedFileCount.Should().Be(1);
        summary.UnidentifiedIndexedFileCount.Should().Be(2);
        summary.OrphanIndexedFileCount.Should().Be(3);
    }

    [Test]
    public async Task Handle_ShouldDeriveLinkedMediaStats_FromAvailabilityNotUnion()
    {
        var libraryId = SeedMovieLibrary();
        var movieId = Guid.NewGuid();

        _context.Medias.Add(new Movie { Id = movieId, Title = "Only In Availability" });
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = libraryId,
            MediaId = movieId
        });
        await _context.SaveChangesAsync();

        var summaries = await _handler.Handle(new GetDiagnosticsSummaryQuery(), CancellationToken.None);

        var summary = summaries.Should().ContainSingle().Subject;
        summary.TotalMediaCount.Should().Be(1);
        summary.MediaMissingPicturesCount.Should().Be(1);
        summary.MediaMissingExternalIdCount.Should().Be(1);
    }

    [Test]
    public async Task GetMissingIntroOutroCounts_ShouldNotRequireOnDiskFiles()
    {
        var libraryId = SeedSerieLibrary();
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episode1 = Guid.NewGuid();
        var episode2 = Guid.NewGuid();

        _context.Medias.AddRange(
            new Serie { Id = serieId, Title = "Serie" },
            new SerieSeason { Id = seasonId, SerieId = serieId, SeasonNumber = 1, Title = "S1" },
            new SerieEpisode
            {
                Id = episode1,
                SerieId = serieId,
                SeasonId = seasonId,
                EpisodeNumber = 1,
                Title = "E1"
            },
            new SerieEpisode
            {
                Id = episode2,
                SerieId = serieId,
                SeasonId = seasonId,
                EpisodeNumber = 2,
                Title = "E2"
            });

        AddProbedEpisodeFile(libraryId, episode1, "/nonexistent/media/serie/s01/e01.mkv");
        AddProbedEpisodeFile(libraryId, episode2, "/nonexistent/media/serie/s01/e02.mkv");
        await _context.SaveChangesAsync();

        var counts = await IntroOutroDiagnosticHelper.GetMissingIntroOutroCountsByLibraryAsync(
            _context, CancellationToken.None);

        counts.Should().ContainKey(libraryId);
        counts[libraryId].Should().Be(2);
    }

    [Test]
    public async Task GetMissingThemeCounts_ShouldNotRequireOnDiskEpisodeFiles()
    {
        var libraryId = SeedSerieLibrary();
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episode1 = Guid.NewGuid();
        var episode2 = Guid.NewGuid();

        _context.Medias.AddRange(
            new Serie { Id = serieId, Title = "Serie" },
            new SerieSeason { Id = seasonId, SerieId = serieId, SeasonNumber = 1, Title = "S1" },
            new SerieEpisode
            {
                Id = episode1,
                SerieId = serieId,
                SeasonId = seasonId,
                EpisodeNumber = 1,
                Title = "E1"
            },
            new SerieEpisode
            {
                Id = episode2,
                SerieId = serieId,
                SeasonId = seasonId,
                EpisodeNumber = 2,
                Title = "E2"
            });

        AddProbedEpisodeFile(libraryId, episode1, "/nonexistent/media/serie/s01/e01.mkv");
        AddProbedEpisodeFile(libraryId, episode2, "/nonexistent/media/serie/s01/e02.mkv");
        await _context.SaveChangesAsync();

        var counts = await ThemeSongDiagnosticHelper.GetMissingThemeCountsByLibraryAsync(
            _context, _paths, CancellationToken.None);

        counts.Should().ContainKey(libraryId);
        counts[libraryId].Should().Be(1);
    }

    private Guid SeedMovieLibrary()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();
        return libraryId;
    }

    private Guid SeedSerieLibrary()
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
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            IntroDetectionEnabled = true,
            ThemeSongGenerationEnabled = true
        });
        _context.SaveChanges();
        return libraryId;
    }

    private void AddProbedEpisodeFile(Guid libraryId, Guid episodeId, string path)
    {
        var fileId = Guid.NewGuid();
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = libraryId,
            MediaId = episodeId,
            Name = Path.GetFileNameWithoutExtension(path),
            Extension = Path.GetExtension(path),
            Path = path,
            ParentDirectory = Path.GetDirectoryName(path) ?? "/media",
            Hash = 1u,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = Guid.NewGuid(),
                IndexedFileId = fileId,
                Container = "matroska",
                Duration = TimeSpan.FromMinutes(42),
                VideoBitrate = 4_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p
            }
        });
    }
}
