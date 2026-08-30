using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleVtt;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Queries;

[TestFixture]
public class GetSubtitleVttQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private IMediaTranscoder _transcoder = null!;
    private GetSubtitleVttQueryHandler _handler = null!;
    private string _mediaFilePath = null!;
    private string _transcodeDir = null!;

    private Guid _indexedFileId;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        await _context.Database.EnsureCreatedAsync();

        _indexedFileId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        _mediaFilePath = Path.Combine(Path.GetTempPath(), $"k7-vtt-{Guid.NewGuid():N}.mkv");
        await File.WriteAllBytesAsync(_mediaFilePath, [0x00]);
        _transcodeDir = Path.Combine(Path.GetTempPath(), "k7-vtt-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_transcodeDir);

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
            RootPath = Path.GetDirectoryName(_mediaFilePath)!,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = _indexedFileId,
            LibraryId = libraryId,
            Name = Path.GetFileNameWithoutExtension(_mediaFilePath),
            Extension = ".mkv",
            Path = _mediaFilePath,
            Hash = 1,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = metadataId,
                Container = "matroska",
                VideoBitrate = 5_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Duration = TimeSpan.FromHours(2),
                SubtitleTracks =
                [
                    new SubtitleFileTrack
                    {
                        Index = 3,
                        Codec = "subrip",
                        Language = "fra",
                        Name = "French",
                        IsTextBased = true
                    },
                    new SubtitleFileTrack
                    {
                        Index = 4,
                        Codec = "hdmv_pgs_subtitle",
                        Language = "fra",
                        Name = "PGS",
                        IsTextBased = false
                    }
                ]
            }
        });
        await _context.SaveChangesAsync();

        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _accessGuard.EnsureAccessByIndexedFileAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _transcoder = Substitute.For<IMediaTranscoder>();

        _handler = new GetSubtitleVttQueryHandler(
            _context,
            _accessGuard,
            _transcoder,
            NullLogger<GetSubtitleVttQueryHandler>.Instance,
            Options.Create(new PathsConfiguration { Transcoding = _transcodeDir }));
    }

    [TearDown]
    public async Task TearDown()
    {
        _context.Dispose();
        await _connection.DisposeAsync();

        if (File.Exists(_mediaFilePath))
            File.Delete(_mediaFilePath);
        if (Directory.Exists(_transcodeDir))
            Directory.Delete(_transcodeDir, recursive: true);
    }

    [Test]
    public async Task Handle_ShouldReturn404_WhenTrackIsMissing()
    {
        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 99),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>();
        ((EmptyHttpContentResult)result).StatusCode.Should().Be(404);
        await _transcoder.DidNotReceiveWithAnyArgs()
            .ExtractSubtitleAsVttAsync(default!, default, default!, default);
    }

    [Test]
    public async Task Handle_ShouldReturn404_WhenTrackIsImageBased()
    {
        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 4),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>();
        ((EmptyHttpContentResult)result).StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Handle_ShouldReturn404_WhenMediaFileMissingOnDisk()
    {
        File.Delete(_mediaFilePath);

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>();
        ((EmptyHttpContentResult)result).StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Handle_ShouldReturnVtt_WhenExtractSucceeds()
    {
        const string vtt = "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHi\n";
        _transcoder.ExtractSubtitleAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var path = call.ArgAt<string>(2);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                return File.WriteAllTextAsync(path, vtt);
            });

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3),
            CancellationToken.None);

        result.Should().BeOfType<TextHttpContentResult>();
        var text = (TextHttpContentResult)result;
        text.Content.Should().Be(vtt);
        text.ContentType.Should().StartWith("text/vtt");
        await _transcoder.Received(1).ExtractSubtitleAsVttAsync(
            _mediaFilePath,
            3,
            HlsSubtitleVttExtractor.GetCachePath(_transcodeDir, _indexedFileId, 3),
            Arg.Any<CancellationToken>());
    }
}
