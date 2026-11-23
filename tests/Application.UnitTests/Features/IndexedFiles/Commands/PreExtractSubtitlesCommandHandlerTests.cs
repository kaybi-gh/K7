using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Features.IndexedFiles.Commands.PreExtractSubtitles;
using K7.Server.Application.Helpers;
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

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class PreExtractSubtitlesCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaTranscoder _transcoder = null!;
    private PreExtractSubtitlesCommandHandler _handler = null!;
    private string _mediaFilePath = null!;
    private string _metadatasDir = null!;
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

        _mediaFilePath = Path.Combine(Path.GetTempPath(), $"k7-presub-{Guid.NewGuid():N}.mkv");
        await File.WriteAllBytesAsync(_mediaFilePath, [0x00]);
        _metadatasDir = Path.Combine(Path.GetTempPath(), "k7-presub-meta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_metadatasDir);

        _transcoder = Substitute.For<IMediaTranscoder>();
        _handler = new PreExtractSubtitlesCommandHandler(
            _context,
            _transcoder,
            Options.Create(new PathsConfiguration { Metadatas = _metadatasDir }),
            NullLogger<PreExtractSubtitlesCommandHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        _context.Dispose();
        await _connection.DisposeAsync();

        if (File.Exists(_mediaFilePath))
            File.Delete(_mediaFilePath);
        if (Directory.Exists(_metadatasDir))
            Directory.Delete(_metadatasDir, recursive: true);
    }

    [Test]
    public async Task Handle_ShouldExtractEveryTextTrackInOnePass_WhenLibraryPreExtractsSubtitles()
    {
        await SeedAsync(subtitlePreExtractionEnabled: true);
        _transcoder.ExtractSubtitlesAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<int, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                foreach (var (trackIndex, path) in call.ArgAt<IReadOnlyDictionary<int, string>>(1))
                    await File.WriteAllTextAsync(path, $"WEBVTT\n\n00:00:01.000 --> 00:00:02.000\ntrack {trackIndex}\n");
            });

        await _handler.Handle(new PreExtractSubtitlesCommand { Id = _indexedFileId }, CancellationToken.None);

        await _transcoder.Received(1).ExtractSubtitlesAsVttAsync(
            _mediaFilePath,
            Arg.Is<IReadOnlyDictionary<int, string>>(outputs => outputs.Keys.Order().SequenceEqual(new[] { 3, 5 })),
            Arg.Any<CancellationToken>());

        foreach (var trackIndex in new[] { 3, 5 })
        {
            var path = HlsSubtitleVttExtractor.GetPersistentCachePath(_metadatasDir, _indexedFileId, trackIndex);
            (await File.ReadAllTextAsync(path)).Should().Contain($"track {trackIndex}");
        }

        // The image track is not convertible to WebVTT, and no temporary file is left behind.
        Directory.GetFiles(HlsSubtitleVttExtractor.GetPersistentCacheDirectory(_metadatasDir, _indexedFileId))
            .Select(Path.GetFileName)
            .Should().BeEquivalentTo("3.vtt", "5.vtt");
    }

    [Test]
    public async Task Handle_ShouldNotExtract_WhenLibraryDoesNotPreExtractSubtitles()
    {
        await SeedAsync(subtitlePreExtractionEnabled: false);

        await _handler.Handle(new PreExtractSubtitlesCommand { Id = _indexedFileId }, CancellationToken.None);

        await _transcoder.DidNotReceiveWithAnyArgs()
            .ExtractSubtitlesAsVttAsync(default!, default!, default);
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenExtractionFails()
    {
        await SeedAsync(subtitlePreExtractionEnabled: true);
        _transcoder.ExtractSubtitlesAsVttAsync(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<int, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("ffmpeg failed")));

        var act = () => _handler.Handle(new PreExtractSubtitlesCommand { Id = _indexedFileId }, CancellationToken.None);

        // The background task must end as failed, not as a silent success.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task SeedAsync(bool subtitlePreExtractionEnabled)
    {
        _indexedFileId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
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
            RootPath = Path.GetDirectoryName(_mediaFilePath)!,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            SubtitlePreExtractionEnabled = subtitlePreExtractionEnabled
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
                    new SubtitleFileTrack { Index = 3, Codec = "subrip", Language = "fra", IsTextBased = true },
                    new SubtitleFileTrack { Index = 4, Codec = "hdmv_pgs_subtitle", Language = "fra", IsTextBased = false },
                    new SubtitleFileTrack { Index = 5, Codec = "ass", Language = "eng", IsTextBased = true }
                ]
            }
        });
        await _context.SaveChangesAsync();
    }
}
