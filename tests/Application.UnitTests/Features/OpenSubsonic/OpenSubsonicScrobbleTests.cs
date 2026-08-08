using FluentAssertions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Devices.Commands.EnsureOpenSubsonicDevice;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Features.OpenSubsonic;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.OpenSubsonic;

[TestFixture]
public class OpenSubsonicScrobbleTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private IActiveStreamTracker _activeStreams = null!;
    private IUser _currentUser = null!;
    private OpenSubsonicService _service = null!;
    private Guid _userId;
    private Guid _trackId;
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
            .Returns(Guid.NewGuid());
        _sender.When(x => x.Send(Arg.Any<UpdatePlaybackProgressCommand>(), Arg.Any<CancellationToken>()))
            .Do(ci => _progressCommands.Add(ci.Arg<UpdatePlaybackProgressCommand>()));
        _sender.Send(Arg.Any<UpdatePlaybackProgressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MediatR.Unit.Value));

        _activeStreams = Substitute.For<IActiveStreamTracker>();
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
    public async Task Scrobble_SubmissionFalse_ShouldStartPlayingWithNewSession()
    {
        var result = await _service.ExecuteAsync(
            "scrobble",
            new Dictionary<string, string[]>
            {
                ["id"] = [_trackId.ToString("D")],
                ["submission"] = ["false"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().ContainSingle();
        var captured = _progressCommands[0];
        captured.MediaId.Should().Be(_trackId);
        captured.State.Should().Be(PlaybackState.Playing);
        captured.Position.Should().Be(0);
        captured.Duration.Should().Be(180);
        captured.SessionId.Should().NotBe(Guid.Empty);
    }

    [Test]
    public async Task Scrobble_SubmissionFalse_ShouldReuseOpenSession_AndPreservePosition()
    {
        var existingSessionId = Guid.NewGuid();
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            UserId = _userId,
            MediaId = _trackId,
            SessionId = existingSessionId,
            ReferenceId = existingSessionId,
            StartedAt = DateTime.UtcNow.AddMinutes(-2),
            LastUpdateAt = DateTime.UtcNow.AddMinutes(-1),
            PositionSeconds = 42,
            DurationSeconds = 180,
            WatchedDurationSeconds = 40,
            State = PlaybackState.Paused
        });
        await _context.SaveChangesAsync();

        var result = await _service.ExecuteAsync(
            "scrobble",
            new Dictionary<string, string[]>
            {
                ["id"] = [_trackId.ToString("D")],
                ["submission"] = ["false"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().ContainSingle();
        _progressCommands[0].SessionId.Should().Be(existingSessionId);
        _progressCommands[0].Position.Should().Be(42);
        _progressCommands[0].State.Should().Be(PlaybackState.Playing);
    }

    [Test]
    public async Task Scrobble_SubmissionFalse_ShouldClosePreviousOpenTrackSession()
    {
        var otherTrackId = Guid.NewGuid();
        var otherSessionId = Guid.NewGuid();
        var artistId = await _context.Medias.OfType<MusicArtist>().Select(a => a.Id).FirstAsync();
        var albumId = await _context.Medias.OfType<MusicAlbum>().Select(a => a.Id).FirstAsync();

        _context.Medias.Add(new MusicTrack
        {
            Id = otherTrackId,
            Title = "Other",
            AlbumId = albumId,
            ArtistId = artistId
        });
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            UserId = _userId,
            MediaId = otherTrackId,
            SessionId = otherSessionId,
            ReferenceId = otherSessionId,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            LastUpdateAt = DateTime.UtcNow.AddMinutes(-1),
            PositionSeconds = 12,
            DurationSeconds = 200,
            WatchedDurationSeconds = 12,
            State = PlaybackState.Playing
        });
        await _context.SaveChangesAsync();

        var result = await _service.ExecuteAsync(
            "scrobble",
            new Dictionary<string, string[]>
            {
                ["id"] = [_trackId.ToString("D")],
                ["submission"] = ["false"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().HaveCount(2);
        _progressCommands.Should().Contain(c =>
            c.MediaId == otherTrackId
            && c.SessionId == otherSessionId
            && c.State == PlaybackState.Ended
            && c.Position == 12);
        _progressCommands.Should().Contain(c =>
            c.MediaId == _trackId && c.State == PlaybackState.Playing);

        // Freeze Playing -> Paused before End so pause gaps are not counted as listen time.
        var closed = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == otherSessionId);
        closed.State.Should().Be(PlaybackState.Paused);
        closed.CompletedAt.Should().BeNull();
    }

    [Test]
    public async Task Scrobble_SubmissionFalse_ShouldCloseOnlyLatestOpenSession_NotOlderAbandonedOnes()
    {
        var artistId = await _context.Medias.OfType<MusicArtist>().Select(a => a.Id).FirstAsync();
        var albumId = await _context.Medias.OfType<MusicAlbum>().Select(a => a.Id).FirstAsync();
        var oldTrackId = Guid.NewGuid();
        var recentTrackId = Guid.NewGuid();
        var oldSessionId = Guid.NewGuid();
        var recentSessionId = Guid.NewGuid();

        _context.Medias.AddRange(
            new MusicTrack { Id = oldTrackId, Title = "Old", AlbumId = albumId, ArtistId = artistId },
            new MusicTrack { Id = recentTrackId, Title = "Recent", AlbumId = albumId, ArtistId = artistId });
        _context.MediaPlaybackSessions.AddRange(
            new MediaPlaybackSession
            {
                UserId = _userId,
                MediaId = oldTrackId,
                SessionId = oldSessionId,
                ReferenceId = oldSessionId,
                StartedAt = DateTime.UtcNow.AddHours(-2),
                LastUpdateAt = DateTime.UtcNow.AddHours(-2),
                PositionSeconds = 5,
                DurationSeconds = 200,
                WatchedDurationSeconds = 5,
                State = PlaybackState.Playing
            },
            new MediaPlaybackSession
            {
                UserId = _userId,
                MediaId = recentTrackId,
                SessionId = recentSessionId,
                ReferenceId = recentSessionId,
                StartedAt = DateTime.UtcNow.AddMinutes(-3),
                LastUpdateAt = DateTime.UtcNow.AddMinutes(-1),
                PositionSeconds = 20,
                DurationSeconds = 200,
                WatchedDurationSeconds = 20,
                State = PlaybackState.Playing
            });
        await _context.SaveChangesAsync();

        var result = await _service.ExecuteAsync(
            "scrobble",
            new Dictionary<string, string[]>
            {
                ["id"] = [_trackId.ToString("D")],
                ["submission"] = ["false"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().HaveCount(2);
        _progressCommands.Should().Contain(c =>
            c.MediaId == recentTrackId && c.SessionId == recentSessionId && c.State == PlaybackState.Ended);
        _progressCommands.Should().NotContain(c => c.SessionId == oldSessionId);

        var old = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == oldSessionId);
        old.State.Should().Be(PlaybackState.Playing);
        old.CompletedAt.Should().BeNull();
    }

    [Test]
    public async Task Scrobble_SubmissionTrue_ShouldEndWithFullDuration()
    {
        var result = await _service.ExecuteAsync(
            "scrobble",
            new Dictionary<string, string[]>
            {
                ["id"] = [_trackId.ToString("D")],
                ["submission"] = ["true"],
                ["c"] = ["Tempus"]
            },
            "listener",
            canWrite: true);

        result.IsFailed.Should().BeFalse();
        _progressCommands.Should().ContainSingle();
        var captured = _progressCommands[0];
        captured.State.Should().Be(PlaybackState.Ended);
        captured.Position.Should().Be(180);
        captured.Duration.Should().Be(180);
    }
}
