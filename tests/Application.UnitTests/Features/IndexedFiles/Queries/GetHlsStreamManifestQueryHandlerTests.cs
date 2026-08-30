using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Queries;

[TestFixture]
public class GetHlsStreamManifestQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private ActiveStreamTracker _streamTracker = null!;
    private IFfmpegCapabilitiesService _ffmpegCapabilities = null!;
    private ISender _sender = null!;
    private GetHlsStreamManifestQueryHandler _handler = null!;
    private string _mediaFilePath = null!;

    private Guid _indexedFileId;
    private Guid _libraryId;

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
        _libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var metadataId = Guid.NewGuid();

        _mediaFilePath = Path.Combine(Path.GetTempPath(), $"k7-hls-{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(_mediaFilePath, [0x00]);

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
            RootPath = Path.GetDirectoryName(_mediaFilePath)!,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = _indexedFileId,
            LibraryId = _libraryId,
            Name = Path.GetFileNameWithoutExtension(_mediaFilePath),
            Extension = ".mp4",
            Path = _mediaFilePath,
            Hash = 1,
            Size = 1,
            FileMetadata = new VideoFileMetadata
            {
                Id = metadataId,
                Container = "mp4",
                VideoBitrate = 5_000_000,
                VideoResolution = VideoResolutionIdentifier._1080p,
                Duration = TimeSpan.FromHours(2),
                AudioTracks =
                [
                    new AudioFileTrack
                    {
                        Index = 0,
                        Codec = "aac",
                        Channels = 2,
                        IsDefault = true,
                        Language = "eng"
                    }
                ],
                VideoTracks =
                [
                    new VideoFileTrack
                    {
                        Index = 0,
                        Codec = "h264",
                        Width = 1920,
                        Height = 1080,
                        Profile = "high",
                        Level = 40,
                        IsDefault = true
                    }
                ]
            }
        });
        await _context.SaveChangesAsync();

        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _accessGuard.EnsureAccessByIndexedFileAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _streamTracker = new ActiveStreamTracker();
        _ffmpegCapabilities = Substitute.For<IFfmpegCapabilitiesService>();
        _sender = Substitute.For<ISender>();

        _handler = new GetHlsStreamManifestQueryHandler(
            _context,
            _accessGuard,
            _streamTracker,
            _ffmpegCapabilities,
            _sender,
            NullLogger<GetHlsStreamManifestQueryHandler>.Instance);
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
    public async Task Handle_ShouldReturn404_WhenMediaFileMissingOnDisk()
    {
        File.Delete(_mediaFilePath);

        var result = await _handler.Handle(new GetHlsStreamManifestQuery
        {
            Id = _indexedFileId,
            StreamSessionId = Guid.NewGuid()
        }, CancellationToken.None);

        result.Should().BeOfType<EmptyHttpContentResult>();
        ((EmptyHttpContentResult)result).StatusCode.Should().Be(404);
    }

    [Test]
    public async Task Handle_ShouldReturnMasterPlaylist_WhenVideoFileExists()
    {
        var sessionId = Guid.NewGuid();

        var result = await _handler.Handle(new GetHlsStreamManifestQuery
        {
            Id = _indexedFileId,
            StreamSessionId = sessionId,
            TranscodingVideoCodec = "h264"
        }, CancellationToken.None);

        result.Should().BeOfType<TextHttpContentResult>();
        var playlist = ((TextHttpContentResult)result).Content;
        playlist.Should().StartWith("#EXTM3U");
        playlist.Should().Contain("#EXT-X-MEDIA:TYPE=AUDIO");
        playlist.Should().Contain("#EXT-X-STREAM-INF");
        playlist.Should().Contain($"streamSessionId={sessionId}");
        playlist.Should().Contain("CODECS=\"avc1.");
        playlist.Should().Contain("mp4a.");
        playlist.Should().Contain("CHANNELS=");
        playlist.Should().Contain("CLOSED-CAPTIONS=NONE");
    }

    [Test]
    public async Task Handle_ShouldOmitAudioFromCodecs_WhenVideoCodecsOnly()
    {
        var result = await _handler.Handle(new GetHlsStreamManifestQuery
        {
            Id = _indexedFileId,
            StreamSessionId = Guid.NewGuid(),
            TranscodingVideoCodec = "h264",
            VideoCodecsOnly = true
        }, CancellationToken.None);

        var playlist = ((TextHttpContentResult)result).Content;
        playlist.Should().Contain("CODECS=\"avc1.");
        playlist.Should().NotContain("mp4a.");
    }

    [Test]
    public async Task Handle_ShouldForceAacAudioUris_WhenVideoCodecsOnlyAndSourceIsEac3()
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstAsync(x => x.Id == _indexedFileId);
        var video = (VideoFileMetadata)entity.FileMetadata!;
        await _context.Entry(video).Collection(v => v.AudioTracks).LoadAsync();
        video.AudioTracks.Clear();
        video.AudioTracks.Add(new AudioFileTrack
        {
            Index = 1,
            Codec = "eac3",
            Channels = 6,
            IsDefault = true,
            Language = "fra",
            Name = "FR VFF"
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetHlsStreamManifestQuery
        {
            Id = _indexedFileId,
            StreamSessionId = Guid.NewGuid(),
            TranscodingVideoCodec = "h264",
            Quality = "720p",
            VideoCodecsOnly = true
        }, CancellationToken.None);

        var playlist = ((TextHttpContentResult)result).Content;
        playlist.Should().Contain("TranscodingAudioCodec=aac");
        playlist.Should().Contain("CODECS=\"avc1.");
        playlist.Should().NotContain("ec-3");
    }

    [Test]
    public async Task Handle_ShouldAdvertiseStartOffsetOnMaster_WhenStartSecondsIsSet()
    {
        var result = await _handler.Handle(new GetHlsStreamManifestQuery
        {
            Id = _indexedFileId,
            StreamSessionId = Guid.NewGuid(),
            TranscodingVideoCodec = "h264",
            StartSeconds = 612.5
        }, CancellationToken.None);

        var playlist = ((TextHttpContentResult)result).Content;
        playlist.Should().Contain("#EXT-X-START:TIME-OFFSET=612.500,PRECISE=NO");
        playlist.Should().Contain("startSeconds=612.500");
    }

    [Test]
    public async Task Handle_ShouldReturnAudioMasterPlaylist_WhenAudioFileExists()
    {
        var audioFileId = Guid.NewGuid();
        var audioMetadataId = Guid.NewGuid();
        var audioPath = Path.Combine(Path.GetTempPath(), $"k7-audio-{Guid.NewGuid():N}.mp3");
        await File.WriteAllBytesAsync(audioPath, [0x00]);

        try
        {
            _context.IndexedFiles.Add(new IndexedFile
            {
                Id = audioFileId,
                LibraryId = _libraryId,
                Name = "track",
                Extension = ".mp3",
                Path = audioPath,
                Hash = 2,
                Size = 1,
                FileMetadata = new AudioFileMetadata
                {
                    Id = audioMetadataId,
                    Container = "mp3",
                    Duration = TimeSpan.FromMinutes(3),
                    AudioTrack = new AudioFileTrack
                    {
                        Index = 0,
                        Codec = "mp3",
                        Channels = 2
                    }
                }
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetHlsStreamManifestQuery
            {
                Id = audioFileId,
                StreamSessionId = Guid.NewGuid()
            }, CancellationToken.None);

            var playlist = ((TextHttpContentResult)result).Content;
            playlist.Should().StartWith("#EXTM3U");
            playlist.Should().Contain("#EXT-X-MEDIA:TYPE=AUDIO");
            playlist.Should().NotContain("#EXT-X-STREAM-INF:RESOLUTION");
        }
        finally
        {
            if (File.Exists(audioPath))
                File.Delete(audioPath);
        }
    }
}
