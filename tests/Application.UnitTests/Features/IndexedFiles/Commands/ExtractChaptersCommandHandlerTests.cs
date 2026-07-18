using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Commands.ExtractChapters;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class ExtractChaptersCommandHandlerTests
{
    private IApplicationDbContext _context = null!;
    private IMediaAnalysisService _mediaAnalysis = null!;
    private ExtractChaptersCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _mediaAnalysis = Substitute.For<IMediaAnalysisService>();
        _handler = new ExtractChaptersCommandHandler(_context, _mediaAnalysis);
    }

    [Test]
    public async Task Handle_ShouldPersistChapters_WhenLibraryExtractionEnabled()
    {
        var fileId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var path = Path.GetTempFileName();
        try
        {
            var videoMetadata = new VideoFileMetadata
            {
                Id = Guid.NewGuid(),
                VideoBitrate = 1,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Container = "matroska",
                Chapters = null
            };
            var indexedFile = new IndexedFile
            {
                Id = fileId,
                LibraryId = libraryId,
                Name = "movie",
                Extension = ".mkv",
                Path = path,
                ParentDirectory = "/",
                Hash = 1,
                Size = 1,
                FileMetadata = videoMetadata
            };
            var library = new Library
            {
                Id = libraryId,
                Title = "Movies",
                MediaType = LibraryMediaType.Movie,
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en",
                LibraryGroupId = Guid.NewGuid(),
                ChapterExtractionEnabled = true
            };

            var files = new List<IndexedFile> { indexedFile }.BuildMockDbSet();
            var libraries = new List<Library> { library }.BuildMockDbSet();
            _context.IndexedFiles.Returns(files);
            _context.Libraries.Returns(libraries);

            var markers = new List<ChapterMarker>
            {
                new() { StartSeconds = 0, Title = "Intro" },
                new() { StartSeconds = 120, Title = "Act 1" }
            };
            _mediaAnalysis.GetChaptersAsync(path, Arg.Any<CancellationToken>()).Returns(markers);

            await _handler.Handle(new ExtractChaptersCommand { Id = fileId }, CancellationToken.None);

            videoMetadata.Chapters.Should().HaveCount(2);
            videoMetadata.Chapters![0].Title.Should().Be("Intro");
            await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Handle_ShouldClearChapters_WhenLibraryExtractionDisabled()
    {
        var fileId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var path = Path.GetTempFileName();
        try
        {
            var videoMetadata = new VideoFileMetadata
            {
                Id = Guid.NewGuid(),
                VideoBitrate = 1,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Container = "matroska",
                Chapters = [new ChapterMarker { StartSeconds = 10, Title = "Old" }]
            };
            var indexedFile = new IndexedFile
            {
                Id = fileId,
                LibraryId = libraryId,
                Name = "movie",
                Extension = ".mkv",
                Path = path,
                ParentDirectory = "/",
                Hash = 1,
                Size = 1,
                FileMetadata = videoMetadata
            };
            var library = new Library
            {
                Id = libraryId,
                Title = "Movies",
                MediaType = LibraryMediaType.Movie,
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en",
                LibraryGroupId = Guid.NewGuid(),
                ChapterExtractionEnabled = false
            };

            var files = new List<IndexedFile> { indexedFile }.BuildMockDbSet();
            var libraries = new List<Library> { library }.BuildMockDbSet();
            _context.IndexedFiles.Returns(files);
            _context.Libraries.Returns(libraries);

            await _handler.Handle(new ExtractChaptersCommand { Id = fileId }, CancellationToken.None);

            videoMetadata.Chapters.Should().BeNull();
            await _mediaAnalysis.DidNotReceive().GetChaptersAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}

[TestFixture]
public class ChapterExtractionHelperTests
{
    [Test]
    public void NeedsExtraction_ShouldBeTrue_WhenChaptersNull()
    {
        var metadata = new VideoFileMetadata
        {
            Id = Guid.NewGuid(),
            VideoBitrate = 1,
            VideoResolution = VideoResolutionIdentifier._1080p,
            Container = "matroska",
            Chapters = null
        };

        ChapterExtractionHelper.NeedsExtraction(metadata).Should().BeTrue();
    }

    [Test]
    public void NeedsExtraction_ShouldBeFalse_WhenChaptersEmptyList()
    {
        var metadata = new VideoFileMetadata
        {
            Id = Guid.NewGuid(),
            VideoBitrate = 1,
            VideoResolution = VideoResolutionIdentifier._1080p,
            Container = "matroska",
            Chapters = []
        };

        ChapterExtractionHelper.NeedsExtraction(metadata).Should().BeFalse();
    }

    [Test]
    public async Task EnsureChaptersAsync_ShouldNoOp_WhenAlreadyExtracted()
    {
        var context = Substitute.For<IApplicationDbContext>();
        var sender = Substitute.For<ISender>();
        var logger = Substitute.For<ILogger>();
        var fileId = Guid.NewGuid();
        var indexedFile = new IndexedFile
        {
            Id = fileId,
            LibraryId = Guid.NewGuid(),
            Name = "movie",
            Extension = ".mkv",
            Path = "/a.mkv",
            ParentDirectory = "/",
            Hash = 1,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = Guid.NewGuid(),
                VideoBitrate = 1,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Container = "matroska",
                Chapters = []
            }
        };
        var files = new List<IndexedFile> { indexedFile }.BuildMockDbSet();
        context.IndexedFiles.Returns(files);

        await ChapterExtractionHelper.EnsureChaptersAsync(context, sender, fileId, logger);

        await sender.DidNotReceive().Send(Arg.Any<ExtractChaptersCommand>(), Arg.Any<CancellationToken>());
    }
}
