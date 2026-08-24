using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class IntroDetectionQueueHelperTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;

    private Guid _libraryId;
    private Guid _seasonId;

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
        _sender = Substitute.For<ISender>();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task TryQueueForEpisodeAsync_ShouldQueue_WhenProbedEpisodeJoinsSeasonWithSegmentsEligiblePeers()
    {
        var episode = await SeedSeasonWithEpisodesAsync(episodeCount: 2, introDetectionEnabled: true, probed: true);

        await IntroDetectionQueueHelper.TryQueueForEpisodeAsync(
            _context,
            _sender,
            episode.Id,
            NullLogger.Instance,
            CancellationToken.None);

        var captured = CapturedTask();
        captured.Should().NotBeNull();
        captured!.Request.Should().BeOfType<DetectMediaSegmentsCommand>()
            .Which.SeasonId.Should().Be(_seasonId);
        captured.TargetEntityId.Should().Be(_seasonId);
    }

    [Test]
    public async Task TryQueueForEpisodeAsync_ShouldNotQueue_WhenEpisodeHasNoProbedFile()
    {
        var episode = await SeedSeasonWithEpisodesAsync(episodeCount: 2, introDetectionEnabled: true, probed: false);

        await IntroDetectionQueueHelper.TryQueueForEpisodeAsync(
            _context,
            _sender,
            episode.Id,
            NullLogger.Instance,
            CancellationToken.None);

        CapturedTask().Should().BeNull();
    }

    [Test]
    public async Task TryQueueForEpisodeAsync_ShouldNotQueue_WhenIntroDetectionDisabled()
    {
        var episode = await SeedSeasonWithEpisodesAsync(episodeCount: 2, introDetectionEnabled: false, probed: true);

        await IntroDetectionQueueHelper.TryQueueForEpisodeAsync(
            _context,
            _sender,
            episode.Id,
            NullLogger.Instance,
            CancellationToken.None);

        CapturedTask().Should().BeNull();
    }

    [Test]
    public async Task TryQueueForEpisodeAsync_ShouldIgnoreNonEpisodeMediaId()
    {
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Inception" };
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();

        await IntroDetectionQueueHelper.TryQueueForEpisodeAsync(
            _context,
            _sender,
            movie.Id,
            NullLogger.Instance,
            CancellationToken.None);

        CapturedTask().Should().BeNull();
    }

    private CreateBackgroundTaskCommand? CapturedTask() =>
        _sender.ReceivedCalls()
            .Select(call => call.GetArguments().FirstOrDefault())
            .OfType<CreateBackgroundTaskCommand>()
            .FirstOrDefault();

    private async Task<SerieEpisode> SeedSeasonWithEpisodesAsync(int episodeCount, bool introDetectionEnabled, bool probed)
    {
        _libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

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
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            IntroDetectionEnabled = introDetectionEnabled
        });

        var serieId = Guid.NewGuid();
        _seasonId = Guid.NewGuid();
        _context.Medias.Add(new Serie { Id = serieId, Title = "Serie" });
        _context.Medias.Add(new SerieSeason { Id = _seasonId, SerieId = serieId, SeasonNumber = 1, Title = "Season 1" });

        SerieEpisode? firstEpisode = null;
        for (var i = 0; i < episodeCount; i++)
        {
            var episode = new SerieEpisode
            {
                Id = Guid.NewGuid(),
                SeasonId = _seasonId,
                SerieId = serieId,
                EpisodeNumber = i + 1,
                Title = $"Episode {i + 1}"
            };
            _context.Medias.Add(episode);
            firstEpisode ??= episode;
        }

        var indexedFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            MediaId = firstEpisode!.Id,
            Name = "episode",
            Extension = ".mkv",
            Path = "/media/episode.mkv",
            ParentDirectory = "/media",
            Hash = 1u,
            Size = 1
        };

        if (probed)
        {
            indexedFile.FileMetadata = new VideoFileMetadata
            {
                Id = Guid.NewGuid(),
                IndexedFileId = indexedFile.Id,
                Container = "matroska",
                Duration = TimeSpan.FromMinutes(42),
                VideoBitrate = 4_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p
            };
        }

        _context.IndexedFiles.Add(indexedFile);
        await _context.SaveChangesAsync();

        return firstEpisode;
    }
}
