using K7.Server.Application.Features.IndexedFiles.Commands.BackfillVideoFrameRate;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class BackfillVideoFrameRateCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaAnalysisService _mediaAnalysis = null!;
    private BackfillVideoFrameRateCommandHandler _handler = null!;

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
        _mediaAnalysis = Substitute.For<IMediaAnalysisService>();
        _handler = new BackfillVideoFrameRateCommandHandler(
            _context,
            _mediaAnalysis,
            NullLogger<BackfillVideoFrameRateCommandHandler>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldPersistProbedFps_WhenTracksAreMissingFrameRate()
    {
        var path = Path.GetTempFileName();
        try
        {
            var fileId = await SeedVideoFileAsync(path, frameRate: null);
            _mediaAnalysis.ProbeVideoFrameRateAsync(path, Arg.Any<CancellationToken>())
                .Returns(23.976f);

            var result = await _handler.Handle(
                new BackfillVideoFrameRateCommand(fileId),
                CancellationToken.None);

            result.Should().ContainSingle().Which.FrameRate.Should().BeApproximately(23.976f, 0.001f);

            var stored = await _context.Set<VideoFileTrack>().AsNoTracking().SingleAsync();
            stored.FrameRate.Should().BeApproximately(23.976f, 0.001f);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Handle_ShouldSkipProbe_WhenFrameRateAlreadyPresent()
    {
        var fileId = await SeedVideoFileAsync("/missing.mkv", frameRate: 24f);

        var result = await _handler.Handle(
            new BackfillVideoFrameRateCommand(fileId),
            CancellationToken.None);

        result.Should().ContainSingle().Which.FrameRate.Should().Be(24f);
        await _mediaAnalysis.DidNotReceive()
            .ProbeVideoFrameRateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldSkipProbe_WhenFileIsMissing()
    {
        var fileId = await SeedVideoFileAsync("/no/such/file.mkv", frameRate: null);

        var result = await _handler.Handle(
            new BackfillVideoFrameRateCommand(fileId),
            CancellationToken.None);

        result.Should().ContainSingle().Which.FrameRate.Should().BeNull();
        await _mediaAnalysis.DidNotReceive()
            .ProbeVideoFrameRateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldLeaveTracksUnchanged_WhenProbeFails()
    {
        var path = Path.GetTempFileName();
        try
        {
            var fileId = await SeedVideoFileAsync(path, frameRate: null);
            _mediaAnalysis.ProbeVideoFrameRateAsync(path, Arg.Any<CancellationToken>())
                .Returns<float?>(_ => throw new InvalidOperationException("ffprobe failed"));

            var result = await _handler.Handle(
                new BackfillVideoFrameRateCommand(fileId),
                CancellationToken.None);

            result.Should().ContainSingle().Which.FrameRate.Should().BeNull();

            var stored = await _context.Set<VideoFileTrack>().AsNoTracking().SingleAsync();
            stored.FrameRate.Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private async Task<Guid> SeedVideoFileAsync(string path, float? frameRate)
    {
        var libraryId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

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
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = libraryId,
            Name = "movie",
            Extension = ".mkv",
            Path = path,
            ParentDirectory = Path.GetDirectoryName(path) ?? "/",
            Hash = 1u,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = metadataId,
                IndexedFileId = fileId,
                Container = "matroska",
                Duration = TimeSpan.FromMinutes(10),
                VideoBitrate = 4_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p,
                VideoTracks =
                [
                    new VideoFileTrack
                    {
                        Index = 0,
                        IsDefault = true,
                        Width = 1920,
                        Height = 802,
                        Codec = "hevc",
                        Profile = "Main 10",
                        Level = 120,
                        FrameRate = frameRate
                    }
                ]
            }
        });
        await _context.SaveChangesAsync();
        return fileId;
    }
}
