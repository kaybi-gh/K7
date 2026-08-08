using FluentAssertions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.OpenSubsonic;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Enums;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.OpenSubsonic;

[TestFixture]
public class OpenSubsonicReportPlaybackTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private ActiveStreamTracker _activeStreams = null!;
    private IUser _currentUser = null!;
    private OpenSubsonicService _service = null!;
    private Guid _userId;
    private Guid _trackId;
    private Guid _deviceId;
    private List<UpdatePlaybackProgressCommand> _progressCommands = null!;

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

        _userId = Guid.NewGuid();
        _trackId = Guid.NewGuid();
        _deviceId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var artistId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "listener" });
        _context.Medias.Add(new MusicArtist { Id = artistId, Title = "Artist" });
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album", ArtistId = artistId });
        _context.Medias.Add(new MusicTrack
        {
            Id = _trackId,
            Title = "Song",
            AlbumId = albumId,
            ArtistId = artistId
        });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            Title = "Music",
            RootPath = "/music",
            MediaType = LibraryMediaType.Music,
            MetadataProviderName = "MusicBrainz",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = groupId
        });
        _context.Devices.Add(new Device
        {
            Id = _deviceId,
            DeviceName = "Tempus",
            ClientType = ClientType.External,
            DeviceType = DeviceType.Phone
        });
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            MediaId = _trackId,
            LibraryId = libraryId,
            Name = "song.flac",
            Extension = ".flac",
            Path = "/music/song.flac",
            Hash = 1,
            Size = 1024,
            FileMetadata = new AudioFileMetadata
            {
                Id = Guid.NewGuid(),
                IndexedFileId = fileId,
                Container = "flac",
                Duration = TimeSpan.FromSeconds(180)
            }
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetIdAsync(Arg.Any<CancellationToken>()).Returns(_userId);
        _currentUser.IdentityId.Returns("ident");

        _progressCommands = [];
        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<EnsureOpenSubsonicDeviceCommand>(), Arg.Any<CancellationToken>())
            .Returns(_deviceId);
        _sender.When(x => x.Send(Arg.Any<UpdatePlaybackProgressCommand>(), Arg.Any<CancellationToken>()))
            .Do(ci => _progressCommands.Add(ci.Arg<UpdatePlaybackProgressCommand>()));
        _sender.Send(Arg.Any<UpdatePlaybackProgressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Unit.Value));

        _activeStreams = new ActiveStreamTracker();
        var accessGuard = Substitute.For<IMediaAccessGuard>();
        var transcoder = Substitute.For<IOpenSubsonicAudioTranscoder>();

        _service = new OpenSubsonicService(
            _context,
            _currentUser,
            accessGuard,
            new MediaAccessFilter(_context),
            _sender,
            _activeStreams,
            transcoder,
            NullLogger<OpenSubsonicService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ReportPlayback_Playing_ShouldSetHasPlaybackProgress()
    {
        var result = await _service.ExecuteAsync(
            "reportPlayback",
            new Dictionary<string, string[]>
            {
                ["mediaId"] = [_trackId.ToString("D")],
                ["mediaType"] = ["song"],
                ["positionMs"] = ["45000"],
                ["state"] = ["playing"],
                ["playbackRate"] = ["1.0"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().ContainSingle();
        _progressCommands[0].State.Should().Be(PlaybackState.Playing);
        _progressCommands[0].Position.Should().BeApproximately(45, 0.01);

        var streams = _activeStreams.GetActiveStreams();
        streams.Should().ContainSingle();
        streams[0].HasPlaybackProgress.Should().BeTrue();
        streams[0].Position.Should().BeApproximately(45, 0.01);
        streams[0].MediaId.Should().Be(_trackId);
    }

    [Test]
    public async Task ReportPlayback_IgnoreScrobble_ShouldNotWriteHistory()
    {
        var result = await _service.ExecuteAsync(
            "reportPlayback",
            new Dictionary<string, string[]>
            {
                ["mediaId"] = [_trackId.ToString("D")],
                ["mediaType"] = ["song"],
                ["positionMs"] = ["10000"],
                ["state"] = ["playing"],
                ["ignoreScrobble"] = ["true"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().BeEmpty();
        _activeStreams.GetActiveStreams().Should().ContainSingle(s => s.HasPlaybackProgress);
    }

    [Test]
    public async Task ReportPlayback_Stopped_ShouldPassRealPosition_NotForceFullDuration()
    {
        var result = await _service.ExecuteAsync(
            "reportPlayback",
            new Dictionary<string, string[]>
            {
                ["mediaId"] = [_trackId.ToString("D")],
                ["mediaType"] = ["song"],
                ["positionMs"] = ["60000"],
                ["state"] = ["stopped"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().ContainSingle();
        _progressCommands[0].State.Should().Be(PlaybackState.Ended);
        _progressCommands[0].Position.Should().BeApproximately(60, 0.01);
        _progressCommands[0].Duration.Should().Be(180);
    }

    [Test]
    public async Task ExecuteAsync_ShouldAdvertiseNewExtensions()
    {
        var result = await _service.ExecuteAsync(
            "getOpenSubsonicExtensions",
            new Dictionary<string, string[]>(),
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        var list = result.Data!["openSubsonicExtensions"] as List<OpenSubsonicExtension>;
        list.Should().NotBeNull();
        list!.Select(e => e.Name).Should().Contain(["formPost", "playbackReport", "transcodeOffset"]);
    }
}
