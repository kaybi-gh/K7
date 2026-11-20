using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class IntroOutroDiagnosticHelperTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

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
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task GetMissingIntroOutroEpisodeIds_ShouldKeepEpisode_WhenLimitIsSingleEpisodeFromEligibleSeason()
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

        // Diagnostic Virtualize pages often contain only one episode of a season.
        var limited = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodeIdsAsync(
            _context,
            libraryId,
            limitToEpisodeIds: [episode1],
            verifyOnDisk: false,
            CancellationToken.None);

        limited.Should().ContainSingle().Which.Should().Be(episode1);
    }

    [Test]
    public async Task GetMissingIntroOutroEpisodes_ShouldReportIntroOnly_WhenOutroExists()
    {
        var libraryId = SeedSerieLibrary();
        var (_, _, episode1, episode2) = SeedTwoEpisodes();

        AddProbedEpisodeFile(libraryId, episode1, "/nonexistent/media/serie/s01/e01.mkv");
        AddProbedEpisodeFile(libraryId, episode2, "/nonexistent/media/serie/s01/e02.mkv");
        _context.MediaSegments.Add(new MediaSegment
        {
            Id = Guid.NewGuid(),
            MediaId = episode1,
            Type = MediaSegmentType.Outro,
            StartMs = 100_000,
            EndMs = 130_000,
            DetectedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var candidates = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodesAsync(
            _context, libraryId, verifyOnDisk: false, cancellationToken: CancellationToken.None);

        var e1 = candidates.Should().ContainSingle(c => c.EpisodeId == episode1).Subject;
        e1.MissingTypes.Should().Equal(IntroOutroSegmentCompleteness.IntroTypeName);

        var e2 = candidates.Should().ContainSingle(c => c.EpisodeId == episode2).Subject;
        e2.MissingTypes.Should().Equal(
            IntroOutroSegmentCompleteness.IntroTypeName,
            IntroOutroSegmentCompleteness.OutroTypeName);
    }

    [Test]
    public async Task GetMissingIntroOutroEpisodes_ShouldReportBoth_WhenNoSegments()
    {
        var libraryId = SeedSerieLibrary();
        var (_, _, episode1, episode2) = SeedTwoEpisodes();

        AddProbedEpisodeFile(libraryId, episode1, "/nonexistent/media/serie/s01/e01.mkv");
        AddProbedEpisodeFile(libraryId, episode2, "/nonexistent/media/serie/s01/e02.mkv");
        await _context.SaveChangesAsync();

        var candidates = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodesAsync(
            _context, libraryId, verifyOnDisk: false, cancellationToken: CancellationToken.None);

        candidates.Should().HaveCount(2);
        foreach (var candidate in candidates)
        {
            candidate.MissingTypes.Should().Equal(
                IntroOutroSegmentCompleteness.IntroTypeName,
                IntroOutroSegmentCompleteness.OutroTypeName);
        }
    }

    [Test]
    public async Task GetMissingIntroOutroEpisodes_ShouldSkipEpisode_WhenCurrentDetectionAlreadyAnalysedIt()
    {
        var libraryId = SeedSerieLibrary();
        var (_, _, episode1, episode2) = SeedTwoEpisodes();

        AddProbedEpisodeFile(libraryId, episode1, "/nonexistent/media/serie/s01/e01.mkv");
        AddProbedEpisodeFile(libraryId, episode2, "/nonexistent/media/serie/s01/e02.mkv");
        await _context.SaveChangesAsync();

        var settled = await _context.Medias.OfType<SerieEpisode>().SingleAsync(e => e.Id == episode1);
        settled.SegmentDetectionVersion = IntroOutroSegmentCompleteness.CurrentDetectionVersion;
        await _context.SaveChangesAsync();

        var candidates = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodesAsync(
            _context, libraryId, verifyOnDisk: false, cancellationToken: CancellationToken.None);

        candidates.Should().ContainSingle().Which.EpisodeId.Should().Be(episode2);
    }

    [Test]
    public async Task GetMissingIntroOutroEpisodes_ShouldReportEpisode_WhenAnalysedByOlderDetection()
    {
        var libraryId = SeedSerieLibrary();
        var (_, _, episode1, episode2) = SeedTwoEpisodes();

        AddProbedEpisodeFile(libraryId, episode1, "/nonexistent/media/serie/s01/e01.mkv");
        AddProbedEpisodeFile(libraryId, episode2, "/nonexistent/media/serie/s01/e02.mkv");
        await _context.SaveChangesAsync();

        var outdated = await _context.Medias.OfType<SerieEpisode>().SingleAsync(e => e.Id == episode1);
        outdated.SegmentDetectionVersion = IntroOutroSegmentCompleteness.CurrentDetectionVersion - 1;
        await _context.SaveChangesAsync();

        var candidates = await IntroOutroDiagnosticHelper.GetMissingIntroOutroEpisodesAsync(
            _context, libraryId, verifyOnDisk: false, cancellationToken: CancellationToken.None);

        candidates.Select(c => c.EpisodeId).Should().BeEquivalentTo([episode1, episode2]);
    }

    private (Guid SerieId, Guid SeasonId, Guid Episode1, Guid Episode2) SeedTwoEpisodes()
    {
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

        return (serieId, seasonId, episode1, episode2);
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
            IntroDetectionEnabled = true
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
