using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class ComputeHlsSegmentsCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaAnalysisService _mediaAnalysis = null!;
    private ComputeHlsSegmentsCommandHandler _handler = null!;

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
        _handler = new ComputeHlsSegmentsCommandHandler(_context, _mediaAnalysis);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldPersistSegmentsWithIndexedFileId()
    {
        var path = Path.GetTempFileName();
        try
        {
            var (fileId, metadataId) = await SeedVideoFileAsync(path);
            _mediaAnalysis.ComputeKeyframeBasedHlsSegmentsAsync(
                    Arg.Any<IndexedFile>(),
                    Arg.Any<TimeSpan>(),
                    Arg.Any<long>(),
                    Arg.Any<CancellationToken>())
                .Returns(call =>
                [
                    new HlsSegment
                    {
                        FileMetadataId = metadataId,
                        IndexedFileId = fileId,
                        Number = 0,
                        StartTimestamp = 0,
                        Duration = 6000
                    }
                ]);

            await _handler.Handle(
                new ComputeHlsSegmentsCommand
                {
                    Id = fileId,
                    SegmentsDuration = TimeSpan.FromMilliseconds(Hls.TargetSegmentDurationMs)
                },
                CancellationToken.None);

            var stored = await _context.HlsSegments.Where(s => s.IndexedFileId == fileId).ToListAsync();
            stored.Should().ContainSingle();
            stored[0].FileMetadataId.Should().Be(metadataId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Handle_ShouldPersistSingleSegment_WhenProbeFindsNoKeyframes()
    {
        var path = Path.GetTempFileName();
        try
        {
            var (fileId, metadataId) = await SeedVideoFileAsync(path);
            _mediaAnalysis.ComputeKeyframeBasedHlsSegmentsAsync(
                    Arg.Any<IndexedFile>(),
                    Arg.Any<TimeSpan>(),
                    Arg.Any<long>(),
                    Arg.Any<CancellationToken>())
                .Returns([]);

            await _handler.Handle(
                new ComputeHlsSegmentsCommand
                {
                    Id = fileId,
                    SegmentsDuration = TimeSpan.FromMilliseconds(Hls.TargetSegmentDurationMs)
                },
                CancellationToken.None);

            var stored = await _context.HlsSegments.Where(s => s.IndexedFileId == fileId).ToListAsync();
            stored.Should().ContainSingle();
            stored[0].StartTimestamp.Should().Be(0);
            stored[0].Duration.Should().Be((long)TimeSpan.FromMinutes(10).TotalMilliseconds);
            stored[0].FileMetadataId.Should().Be(metadataId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private async Task<(Guid FileId, Guid MetadataId)> SeedVideoFileAsync(string path)
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
            MetadataFallbackLanguage = "en",
            TransmuxingEnabled = true
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
                VideoResolution = VideoResolutionIdentifier._1080p
            }
        });
        await _context.SaveChangesAsync();
        return (fileId, metadataId);
    }
}
