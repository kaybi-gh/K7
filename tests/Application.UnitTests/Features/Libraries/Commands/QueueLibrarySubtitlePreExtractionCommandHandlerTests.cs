using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using MediatR;
using K7.Server.Application.Features.IndexedFiles.Commands.PreExtractSubtitles;
using K7.Server.Application.Features.Libraries.Commands.QueueLibrarySubtitlePreExtraction;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.Libraries.Commands;

[TestFixture]
public class QueueLibrarySubtitlePreExtractionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private QueueLibrarySubtitlePreExtractionCommandHandler _handler = null!;
    private Guid _libraryId;
    private Guid _textFileId;

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

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTasksBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        _handler = new QueueLibrarySubtitlePreExtractionCommandHandler(
            _context,
            _sender,
            NullLogger<QueueLibrarySubtitlePreExtractionCommandHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        _context.Dispose();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task Handle_ShouldQueueTextSubtitleFilesOnly_WhenPreExtractionIsEnabled()
    {
        await SeedAsync(subtitlePreExtractionEnabled: true);

        var queued = await _handler.Handle(new QueueLibrarySubtitlePreExtractionCommand(_libraryId), CancellationToken.None);

        queued.Should().Be(1);
        await _sender.Received(1).Send(
            Arg.Is<CreateBackgroundTasksBatchCommand>(batch =>
                batch.Items.Count == 1
                && batch.Items[0].TargetEntityId == _textFileId
                && batch.Items[0].Request is PreExtractSubtitlesCommand
                && batch.Items[0].TimeoutSeconds == PreExtractSubtitlesCommand.TimeoutSeconds),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldQueueNothing_WhenPreExtractionIsDisabled()
    {
        await SeedAsync(subtitlePreExtractionEnabled: false);

        var queued = await _handler.Handle(new QueueLibrarySubtitlePreExtractionCommand(_libraryId), CancellationToken.None);

        queued.Should().Be(0);
        await _sender.DidNotReceiveWithAnyArgs()
            .Send(default(CreateBackgroundTasksBatchCommand)!, default);
    }

    private async Task SeedAsync(bool subtitlePreExtractionEnabled)
    {
        _libraryId = Guid.NewGuid();
        _textFileId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var otherLibraryId = Guid.NewGuid();

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = Path.GetTempPath(),
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            SubtitlePreExtractionEnabled = subtitlePreExtractionEnabled
        });
        _context.Libraries.Add(new Library
        {
            Id = otherLibraryId,
            LibraryGroupId = groupId,
            Title = "Other",
            MediaType = LibraryMediaType.Movie,
            RootPath = Path.GetTempPath(),
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            SubtitlePreExtractionEnabled = true
        });

        _context.IndexedFiles.Add(VideoFile(_textFileId, _libraryId, "text.mkv",
        [
            new SubtitleFileTrack { Index = 2, Codec = "subrip", Language = "fra", IsTextBased = true },
            new SubtitleFileTrack { Index = 3, Codec = "hdmv_pgs_subtitle", Language = "fra", IsTextBased = false }
        ]));
        _context.IndexedFiles.Add(VideoFile(Guid.NewGuid(), _libraryId, "pgs.mkv",
        [
            new SubtitleFileTrack { Index = 2, Codec = "hdmv_pgs_subtitle", Language = "fra", IsTextBased = false }
        ]));
        _context.IndexedFiles.Add(VideoFile(Guid.NewGuid(), otherLibraryId, "other.mkv",
        [
            new SubtitleFileTrack { Index = 2, Codec = "subrip", Language = "eng", IsTextBased = true }
        ]));

        await _context.SaveChangesAsync();
    }

    private static IndexedFile VideoFile(Guid id, Guid libraryId, string name, SubtitleFileTrack[] tracks) => new()
    {
        Id = id,
        LibraryId = libraryId,
        Name = name,
        Extension = ".mkv",
        Path = Path.Combine(Path.GetTempPath(), name),
        Hash = 1,
        Size = 1,
        FileMetadata = new VideoFileMetadata
        {
            Id = Guid.NewGuid(),
            Container = "matroska",
            VideoBitrate = 1,
            VideoResolution = VideoResolutionIdentifier._1080p,
            Duration = TimeSpan.FromHours(1),
            SubtitleTracks = tracks
        }
    };
}
