using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleSup;
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

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Queries;

[TestFixture]
public class GetSubtitleSupQueryHandlerTests
{
    private const int TextTrackIndex = 3;
    private const int PgsTrackIndex = 4;
    private const int VobSubTrackIndex = 5;

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaTranscoder _transcoder = null!;
    private GetSubtitleSupQueryHandler _handler = null!;
    private string _mediaFilePath = null!;
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

        _mediaFilePath = Path.Combine(Path.GetTempPath(), $"k7-sup-{Guid.NewGuid():N}.mkv");
        await File.WriteAllBytesAsync(_mediaFilePath, [0x00]);

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
                Id = Guid.NewGuid(),
                Container = "matroska",
                VideoBitrate = 5_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Duration = TimeSpan.FromHours(2),
                SubtitleTracks =
                [
                    new SubtitleFileTrack { Index = TextTrackIndex, Codec = "subrip", IsTextBased = true },
                    new SubtitleFileTrack { Index = PgsTrackIndex, Codec = "hdmv_pgs_subtitle", IsTextBased = false },
                    new SubtitleFileTrack { Index = VobSubTrackIndex, Codec = "dvd_subtitle", IsTextBased = false }
                ]
            }
        });
        await _context.SaveChangesAsync();

        var accessGuard = Substitute.For<IMediaAccessGuard>();
        accessGuard.EnsureAccessByIndexedFileAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _transcoder = Substitute.For<IMediaTranscoder>();

        _handler = new GetSubtitleSupQueryHandler(
            _context,
            accessGuard,
            _transcoder,
            NullLogger<GetSubtitleSupQueryHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        _context.Dispose();
        await _connection.DisposeAsync();

        if (File.Exists(_mediaFilePath))
            File.Delete(_mediaFilePath);
    }

    [Test]
    public async Task Handle_ShouldStreamTheWindow_WhenTrackIsPgs()
    {
        byte[] sup = [(byte)'P', (byte)'G', 1, 2, 3];
        _transcoder.ExtractPgsSubtitleWindowAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns(async call => await call.ArgAt<Stream>(4).WriteAsync(sup));

        var result = await _handler.Handle(
            new GetSubtitleSupQuery(_indexedFileId, PgsTrackIndex, 3590.5, 120),
            CancellationToken.None);

        var stream = result.Should().BeOfType<StreamHttpContentResult>().Subject;
        using var body = new MemoryStream();
        await stream.OpenStream().CopyToAsync(body);
        body.ToArray().Should().Equal(sup);
        await _transcoder.Received(1).ExtractPgsSubtitleWindowAsync(
            _mediaFilePath,
            PgsTrackIndex,
            TimeSpan.FromSeconds(3590.5),
            TimeSpan.FromSeconds(120),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldCapTheWindow_WhenDurationIsTooLong()
    {
        var result = await _handler.Handle(
            new GetSubtitleSupQuery(_indexedFileId, PgsTrackIndex, -5, 99999),
            CancellationToken.None);

        var stream = result.Should().BeOfType<StreamHttpContentResult>().Subject;
        await stream.OpenStream().CopyToAsync(Stream.Null);
        await _transcoder.Received(1).ExtractPgsSubtitleWindowAsync(
            _mediaFilePath,
            PgsTrackIndex,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(GetSubtitleSupQueryHandler.MaxWindowSeconds),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldEndTheBody_WhenExtractionFails()
    {
        _transcoder.ExtractPgsSubtitleWindowAsync(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<Stream>(),
                Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("ffmpeg"));

        var result = await _handler.Handle(
            new GetSubtitleSupQuery(_indexedFileId, PgsTrackIndex, 0, 120),
            CancellationToken.None);

        using var body = new MemoryStream();
        await ((StreamHttpContentResult)result).OpenStream().CopyToAsync(body);
        body.Length.Should().Be(0);
    }

    [TestCase(TextTrackIndex)]
    [TestCase(VobSubTrackIndex)]
    [TestCase(99)]
    public async Task Handle_ShouldReturn404_WhenTrackIsNotPgs(int trackIndex)
    {
        var result = await _handler.Handle(
            new GetSubtitleSupQuery(_indexedFileId, trackIndex, 0, 120),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>().Which.StatusCode.Should().Be(404);
        await _transcoder.DidNotReceiveWithAnyArgs()
            .ExtractPgsSubtitleWindowAsync(default!, default, default, default, default!, default);
    }

    [Test]
    public async Task Handle_ShouldReturn404_WhenFileIsNotLocal()
    {
        var result = await _handler.Handle(
            new GetSubtitleSupQuery(Guid.NewGuid(), PgsTrackIndex, 0, 120),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>().Which.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Handle_ShouldReturn404_WhenMediaFileMissingOnDisk()
    {
        File.Delete(_mediaFilePath);

        var result = await _handler.Handle(
            new GetSubtitleSupQuery(_indexedFileId, PgsTrackIndex, 0, 120),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>().Which.StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Handle_ShouldReturn400_WhenWindowIsEmpty()
    {
        var result = await _handler.Handle(
            new GetSubtitleSupQuery(_indexedFileId, PgsTrackIndex, 10, 0),
            CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>().Which.StatusCode.Should().Be(400);
    }
}
