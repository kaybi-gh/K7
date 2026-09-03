using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class UpdatePlaybackProgressCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private IActiveStreamTracker _tracker = null!;
    private IUserMediaStateUpdater _stateUpdater = null!;
    private ISharedProfileMediaStateUpdater _sharedProfileStateUpdater = null!;
    private ISharedProfilePlaybackResolver _sharedProfiles = null!;
    private IPlaybackPolicySettingsProvider _policies = null!;
    private IPlaybackProgressNotifier _notifier = null!;
    private IIdentityService _identityService = null!;
    private UpdatePlaybackProgressCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _movieId;

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
        _movieId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "viewer" });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("ident");

        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _tracker = Substitute.For<IActiveStreamTracker>();
        _tracker.GetStreamInfo(Arg.Any<Guid>()).Returns((ActiveStreamInfo?)null);

        _stateUpdater = Substitute.For<IUserMediaStateUpdater>();
        _stateUpdater.ApplyAsync(
                Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new UserMediaStateUpdateResult(50, false, false, null));

        _policies = Substitute.For<IPlaybackPolicySettingsProvider>();
        _policies.GetEffectiveVideoPolicyAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new VideoPlaybackPolicySettingsDto { CompletedThresholdPercent = 90 });
        _policies.GetEffectiveAudioPolicyAsync(Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new AudioPlaybackPolicySettingsDto
            {
                CompletedThresholdPercent = 50,
                CompletedMinDurationSeconds = 240
            });

        _notifier = Substitute.For<IPlaybackProgressNotifier>();

        _sharedProfiles = Substitute.For<ISharedProfilePlaybackResolver>();
        _sharedProfiles.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((SharedProfilePlaybackContext?)null);

        _sharedProfileStateUpdater = Substitute.For<ISharedProfileMediaStateUpdater>();
        _sharedProfileStateUpdater.ApplyAsync(
                Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new SharedProfileMediaStateUpdateResult(50, false, false, null));

        var syncPlay = Substitute.For<ISyncPlayPlaybackContextResolver>();
        syncPlay.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((SyncPlayPlaybackContext?)null);

        _identityService = Substitute.For<IIdentityService>();
        _identityService.IsInRoleAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        _handler = new UpdatePlaybackProgressCommandHandler(
            _context,
            _currentUser,
            _notifier,
            _accessGuard,
            _tracker,
            _identityService,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            _stateUpdater,
            _sharedProfileStateUpdater,
            _policies,
            _sharedProfiles,
            syncPlay,
            Substitute.For<IFfmpegCapabilitiesService>(),
            Substitute.For<ILogger<UpdatePlaybackProgressCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateSession_WhenNoneExists()
    {
        var sessionId = Guid.NewGuid();

        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId,
            sessionId,
            Guid.NewGuid(),
            Position: 10,
            Duration: 100,
            State: PlaybackState.Playing), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.MediaId.Should().Be(_movieId);
        session.PositionSeconds.Should().Be(10);
        session.CompletedAt.Should().BeNull();
        await _accessGuard.Received(1).EnsureAccessAsync(_movieId, Arg.Any<CancellationToken>());
        _tracker.Received(1).Upsert(sessionId, Arg.Any<ActiveStreamInfo>());
    }

    [Test]
    public async Task Handle_ShouldMarkCompleted_WhenVideoThresholdReached()
    {
        var sessionId = Guid.NewGuid();

        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId,
            sessionId,
            Guid.NewGuid(),
            Position: 91,
            Duration: 100,
            State: PlaybackState.Playing), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ShouldMarkMusicCompleted_WhenMinDurationReached()
    {
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });
        _context.Medias.Add(new MusicTrack { Id = trackId, Title = "Song", AlbumId = albumId });
        await _context.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            trackId,
            sessionId,
            Guid.NewGuid(),
            Position: 240,
            Duration: 600,
            State: PlaybackState.Playing), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ShouldIncrementPlayCount_WhenMusicRelistenCompletes()
    {
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });
        _context.Medias.Add(new MusicTrack { Id = trackId, Title = "Song", AlbumId = albumId });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _userId,
            MediaId = trackId,
            PlayCount = 1,
            IsCompleted = true,
            LastInteractedAt = DateTime.UtcNow.AddDays(-1)
        });
        await _context.SaveChangesAsync();

        _stateUpdater.ApplyAsync(
                Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new UserMediaStateUpdateResult(100, true, WasNewlyCompleted: false, null));

        var sessionId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            trackId,
            sessionId,
            Guid.NewGuid(),
            Position: 180,
            Duration: 180,
            State: PlaybackState.Ended), CancellationToken.None);

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == _userId && s.MediaId == trackId);
        state.PlayCount.Should().Be(2);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ShouldIncrementSkipCount_WhenEndingWithLittleProgress()
    {
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Album" });
        _context.Medias.Add(new MusicTrack { Id = trackId, Title = "Song", AlbumId = albumId });
        await _context.SaveChangesAsync();

        var sessionId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            trackId,
            sessionId,
            Guid.NewGuid(),
            Position: 0,
            Duration: 200,
            State: PlaybackState.Playing), CancellationToken.None);

        await _handler.Handle(new UpdatePlaybackProgressCommand(
            trackId,
            sessionId,
            Guid.NewGuid(),
            Position: 8,
            Duration: 200,
            State: PlaybackState.Ended), CancellationToken.None);

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == _userId && s.MediaId == trackId);
        state.SkipCount.Should().Be(1);
        state.PlayCount.Should().Be(0);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.CompletedAt.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldUpdateExistingSessionWithoutDoubleComplete()
    {
        var sessionId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId, sessionId, referenceId, 95, 100, PlaybackState.Playing), CancellationToken.None);

        var firstCompletedAt = (await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId)).CompletedAt;

        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId, sessionId, referenceId, 98, 100, PlaybackState.Ended), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.PositionSeconds.Should().Be(98);
        session.State.Should().Be(PlaybackState.Ended);
        session.CompletedAt.Should().Be(firstCompletedAt);
        _tracker.Received(1).Remove(sessionId);
    }

    [Test]
    public async Task Handle_ShouldNoOp_WhenUserUnauthenticated()
    {
        _currentUser.Id.Returns((Guid?)null);

        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId, Guid.NewGuid(), Guid.NewGuid(), 1, 10, PlaybackState.Playing), CancellationToken.None);

        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(0);
        await _accessGuard.DidNotReceive().EnsureAccessAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldCreateSessionWithoutUserMediaState_WhenGuest()
    {
        _identityService.IsInRoleAsync("ident", Roles.Guest).Returns(true);

        var sessionId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId,
            sessionId,
            Guid.NewGuid(),
            Position: 10,
            Duration: 100,
            State: PlaybackState.Playing), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.UserId.Should().Be(_userId);
        session.PositionSeconds.Should().Be(10);
        _tracker.Received(1).Upsert(sessionId, Arg.Any<ActiveStreamInfo>());

        await _stateUpdater.DidNotReceive().ApplyAsync(
            Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceive().NotifyProgressUpdatedAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<bool>(), Arg.Any<MediaType>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldOnlyUpdateSharedProfileMediaState_AndNotHostOrCoviewerUserMediaState_WhenSharedProfileActive()
    {
        var sharedProfileId = Guid.NewGuid();
        var coViewerId = Guid.NewGuid();
        _context.Users.Add(new User { Id = coViewerId, IdentityUserId = "co-ident", DisplayName = "coviewer" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Family",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        _sharedProfiles.ResolveAsync(sharedProfileId, _userId, Arg.Any<CancellationToken>())
            .Returns(new SharedProfilePlaybackContext(sharedProfileId, "Family", [coViewerId]));

        var sessionId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId,
            sessionId,
            Guid.NewGuid(),
            Position: 10,
            Duration: 100,
            State: PlaybackState.Playing,
            SharedProfileId: sharedProfileId), CancellationToken.None);

        await _sharedProfileStateUpdater.Received(1).ApplyAsync(
            sharedProfileId, Arg.Any<BaseMedia>(), _movieId, 10, 100, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        await _stateUpdater.DidNotReceive().ApplyAsync(
            Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.SharedProfileId.Should().Be(sharedProfileId);
        (await _context.UserMediaStates.CountAsync()).Should().Be(0);
        (await _context.MediaPlaybackSessionCoViewers.SingleAsync()).UserId.Should().Be(coViewerId);
    }

    [Test]
    public async Task Handle_ShouldMarkAllSharedProfileMembersWatched_WhenSharedSessionCompletes()
    {
        var sharedProfileId = Guid.NewGuid();
        var coViewerId = Guid.NewGuid();
        _context.Users.Add(new User { Id = coViewerId, IdentityUserId = "co-ident", DisplayName = "coviewer" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Family",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        _sharedProfiles.ResolveAsync(sharedProfileId, _userId, Arg.Any<CancellationToken>())
            .Returns(new SharedProfilePlaybackContext(sharedProfileId, "Family", [coViewerId]));

        _sharedProfileStateUpdater.ApplyAsync(
                Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
                Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new SharedProfileMediaStateUpdateResult(100, true, true, null));

        var sessionId = Guid.NewGuid();
        await _handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId,
            sessionId,
            Guid.NewGuid(),
            Position: 95,
            Duration: 100,
            State: PlaybackState.Ended,
            SharedProfileId: sharedProfileId), CancellationToken.None);

        await _stateUpdater.DidNotReceive().ApplyAsync(
            Arg.Any<Guid>(), Arg.Any<BaseMedia>(), Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>());

        var states = await _context.UserMediaStates.AsNoTracking().ToListAsync();
        states.Should().HaveCount(2);
        states.Should().OnlyContain(s =>
            s.MediaId == _movieId
            && s.IsCompleted
            && s.PlayCount == 1);
        states.Select(s => s.UserId).Should().BeEquivalentTo([_userId, coViewerId]);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.CompletedAt.Should().NotBeNull();
        (await _context.MediaPlaybackSessionCoViewers.SingleAsync()).UserId.Should().Be(coViewerId);
    }

    [Test]
    public async Task Handle_ShouldNotInsertDuplicateSharedProfileMediaState_WhenFirstProgressIsIdleSkip()
    {
        var sharedProfileId = Guid.NewGuid();
        var coViewerId = Guid.NewGuid();
        _context.Users.Add(new User { Id = coViewerId, IdentityUserId = "co-ident", DisplayName = "coviewer" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Family",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();

        _sharedProfiles.ResolveAsync(sharedProfileId, _userId, Arg.Any<CancellationToken>())
            .Returns(new SharedProfilePlaybackContext(sharedProfileId, "Family", [coViewerId]));

        var handler = new UpdatePlaybackProgressCommandHandler(
            _context,
            _currentUser,
            _notifier,
            _accessGuard,
            _tracker,
            _identityService,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            _stateUpdater,
            new SharedProfileMediaStateUpdater(_context, _policies, Substitute.For<IPlaybackBookmarkService>()),
            _policies,
            _sharedProfiles,
            Substitute.For<ISyncPlayPlaybackContextResolver>(),
            Substitute.For<IFfmpegCapabilitiesService>(),
            Substitute.For<ILogger<UpdatePlaybackProgressCommandHandler>>());

        var sessionId = Guid.NewGuid();
        await handler.Handle(new UpdatePlaybackProgressCommand(
            _movieId,
            sessionId,
            Guid.NewGuid(),
            Position: 0,
            Duration: 7547.123,
            State: PlaybackState.Idle,
            SharedProfileId: sharedProfileId), CancellationToken.None);

        var states = await _context.SharedProfileMediaStates.ToListAsync();
        states.Should().HaveCount(1);
        states[0].SkipCount.Should().Be(1);
        states[0].PlayCount.Should().Be(0);

        var session = await _context.MediaPlaybackSessions.SingleAsync(s => s.SessionId == sessionId);
        session.State.Should().Be(PlaybackState.Idle);
        session.CompletedAt.Should().BeNull();
    }
}
