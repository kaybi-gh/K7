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
using K7.Shared.Constants;
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
    private string _metadatasDir = null!;
    private TaskCompletionSource _fullExtractionGate = null!;

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
        _metadatasDir = Path.Combine(Path.GetTempPath(), "k7-vtt-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_metadatasDir);
        _fullExtractionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
            Options.Create(new PathsConfiguration { Transcoding = _transcodeDir, Metadatas = _metadatasDir }),
            Substitute.For<IPeerAuthorizationService>(),
            Substitute.For<IPeerClient>());
    }

    [TearDown]
    public async Task TearDown()
    {
        // Lets a stubbed full extraction end, so nothing keeps running after the test.
        _fullExtractionGate.TrySetResult();
        _context.Dispose();
        await _connection.DisposeAsync();

        if (File.Exists(_mediaFilePath))
            File.Delete(_mediaFilePath);
        if (Directory.Exists(_transcodeDir))
            Directory.Delete(_transcodeDir, recursive: true);
        if (Directory.Exists(_metadatasDir))
            Directory.Delete(_metadatasDir, recursive: true);
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
    public async Task Handle_ShouldReturn503AndStartBackgroundExtract_WhenCacheMissing()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _transcoder.ExtractSubtitleAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.SetResult();
                await Task.Delay(50);
                var path = call.ArgAt<string>(2);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, "WEBVTT\n\n");
            });

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3),
            CancellationToken.None);

        result.Should().BeOfType<TextHttpContentResult>();
        ((TextHttpContentResult)result).StatusCode.Should().Be(503);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // Extracted next to the cache path, then renamed: a reader never sees a half-written file.
        var cachePath = HlsSubtitleVttExtractor.GetCachePath(_transcodeDir, _indexedFileId, 3);
        await _transcoder.Received(1).ExtractSubtitleAsVttAsync(
            _mediaFilePath,
            3,
            Arg.Is<string>(path => path.StartsWith(cachePath, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await _transcoder.DidNotReceiveWithAnyArgs()
            .ExtractSubtitleWindowAsVttAsync(default!, default, default, default, default!, default);
        await WaitUntilAsync(() => HlsSubtitleVttExtractor.IsReady(cachePath));
    }

    [Test]
    public async Task Handle_ShouldReturn503AndExtractWindow_WhenPositionGivenAndWindowMissing()
    {
        StubFullExtractionThatNeverEnds();
        var windowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var windowRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _transcoder.ExtractSubtitleWindowAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                windowStarted.TrySetResult();
                await windowRelease.Task;
            });

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3, PositionSeconds: 3700),
            CancellationToken.None);

        ((TextHttpContentResult)result).StatusCode.Should().Be(503);
        await windowStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        // 3700 s is in window 30 (3600-3720): extracted from 30 s before it to its end.
        await _transcoder.Received(1).ExtractSubtitleWindowAsVttAsync(
            _mediaFilePath,
            3,
            TimeSpan.FromSeconds(3570),
            TimeSpan.FromSeconds(3720),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        windowRelease.SetResult();
    }

    [Test]
    public async Task Handle_ShouldReturnPartialVtt_WhenPositionGivenAndWindowReady()
    {
        StubFullExtractionThatNeverEnds();
        await WriteWindowAsync(30, "WEBVTT\n\n01:01:05.000 --> 01:01:07.000\nNow\n");

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3, PositionSeconds: 3665),
            CancellationToken.None);

        var text = result.Should().BeOfType<TextHttpContentResult>().Subject;
        text.StatusCode.Should().Be(200);
        text.ContentType.Should().StartWith("text/vtt");
        SubtitleVttFormat.IsPartial(text.Content).Should().BeTrue();
        text.Content.Should().Contain("01:01:05.000 --> 01:01:07.000");
    }

    [Test]
    public async Task Handle_ShouldReturnCompleteVtt_WhenPositionGivenAndCacheReady()
    {
        const string vtt = "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHi\n";
        var cachePath = HlsSubtitleVttExtractor.GetCachePath(_transcodeDir, _indexedFileId, 3);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllTextAsync(cachePath, vtt);
        await WriteWindowAsync(0, "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nWindow\n");

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3, PositionSeconds: 1),
            CancellationToken.None);

        var text = result.Should().BeOfType<TextHttpContentResult>().Subject;
        text.Content.Should().Be(vtt);
        SubtitleVttFormat.IsPartial(text.Content).Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnPreExtractedVtt_WhenPersistentCacheReady()
    {
        const string vtt = "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nKept\n";
        var persistentPath = HlsSubtitleVttExtractor.GetPersistentCachePath(_metadatasDir, _indexedFileId, 3);
        Directory.CreateDirectory(Path.GetDirectoryName(persistentPath)!);
        await File.WriteAllTextAsync(persistentPath, vtt);

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3),
            CancellationToken.None);

        result.Should().BeOfType<TextHttpContentResult>().Which.Content.Should().Be(vtt);
        await _transcoder.DidNotReceiveWithAnyArgs()
            .ExtractSubtitleAsVttAsync(default!, default, default!, default);
    }

    [Test]
    public async Task Handle_ShouldExtractToPersistentCache_WhenLibraryPreExtractsSubtitles()
    {
        var library = await _context.Libraries.SingleAsync();
        library.SubtitlePreExtractionEnabled = true;
        await _context.SaveChangesAsync();
        _transcoder.ExtractSubtitleAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call => File.WriteAllTextAsync(call.ArgAt<string>(2), "WEBVTT\n\n"));

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3),
            CancellationToken.None);

        ((TextHttpContentResult)result).StatusCode.Should().Be(503);
        var persistentPath = HlsSubtitleVttExtractor.GetPersistentCachePath(_metadatasDir, _indexedFileId, 3);
        await WaitUntilAsync(() => HlsSubtitleVttExtractor.IsReady(persistentPath));
        HlsSubtitleVttExtractor.IsReady(persistentPath).Should().BeTrue();
        HlsSubtitleVttExtractor.IsReady(HlsSubtitleVttExtractor.GetCachePath(_transcodeDir, _indexedFileId, 3))
            .Should().BeFalse();
    }

    private void StubFullExtractionThatNeverEnds() =>
        _transcoder.ExtractSubtitleAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => _fullExtractionGate.Task);

    private async Task WriteWindowAsync(int windowIndex, string vtt)
    {
        var windowPath = HlsSubtitleVttExtractor.GetWindowCachePath(_transcodeDir, _indexedFileId, 3, windowIndex);
        Directory.CreateDirectory(Path.GetDirectoryName(windowPath)!);
        await File.WriteAllTextAsync(windowPath, vtt);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
    }

    [Test]
    public async Task Handle_ShouldReturnVtt_WhenCacheReady()
    {
        const string vtt = "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\nHi\n";
        var cachePath = HlsSubtitleVttExtractor.GetCachePath(_transcodeDir, _indexedFileId, 3);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        await File.WriteAllTextAsync(cachePath, vtt);

        var result = await _handler.Handle(
            new GetSubtitleVttQuery(_indexedFileId, 3),
            CancellationToken.None);

        result.Should().BeOfType<TextHttpContentResult>();
        var text = (TextHttpContentResult)result;
        text.Content.Should().Be(vtt);
        text.ContentType.Should().StartWith("text/vtt");
        await _transcoder.DidNotReceiveWithAnyArgs()
            .ExtractSubtitleAsVttAsync(default!, default, default!, default);
    }
}
