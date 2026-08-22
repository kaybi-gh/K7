using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Stats.Commands.ReassignPlaybackHistoryItem;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Stats.Commands;

[TestFixture]
public class ReassignPlaybackHistoryItemCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IIdentityService _identityService = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private IPlaybackProgressNotifier _notifier = null!;
    private ReassignPlaybackHistoryItemCommandHandler _handler = null!;
    private Guid _hostId;
    private Guid _partnerId;
    private Guid _movieId;
    private Guid _sharedProfileId;

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

        _hostId = Guid.NewGuid();
        _partnerId = Guid.NewGuid();
        _movieId = Guid.NewGuid();
        _sharedProfileId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _hostId, IdentityUserId = "host", DisplayName = "host" },
            new User { Id = _partnerId, IdentityUserId = "partner", DisplayName = "partner" });
        _context.Medias.Add(new Movie { Id = _movieId, Title = "Film" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Couple",
            HostUserId = _hostId,
            CreatedByUserId = _hostId,
            Members =
            [
                new SharedProfileMember { SharedProfileId = _sharedProfileId, UserId = _hostId },
                new SharedProfileMember { SharedProfileId = _sharedProfileId, UserId = _partnerId }
            ]
        });
        _context.SaveChanges();

        GrantCapability(_hostId, Capability.CanReassignHistory);
        GrantCapability(_partnerId, Capability.CanReassignHistory);
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_hostId);
        _identityService = Substitute.For<IIdentityService>();
        _identityService.GetRolesAsync("host").Returns([Roles.User]);
        _identityService.GetRolesAsync("partner").Returns([Roles.User]);
        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _notifier = Substitute.For<IPlaybackProgressNotifier>();
        _handler = new ReassignPlaybackHistoryItemCommandHandler(
            _context,
            _currentUser,
            _identityService,
            _cacheInvalidator,
            Substitute.For<IPlaybackBookmarkService>(),
            _notifier);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldTagSessionAndCreditPartner_WhenAssigningPersonalToShared()
    {
        var referenceId = AddSession(_hostId, sharedProfileId: null, completed: true);

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, _sharedProfileId),
            CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync();
        session.SharedProfileId.Should().Be(_sharedProfileId);
        session.SharedProfileNameSnapshot.Should().Be("Couple");

        (await _context.MediaPlaybackSessionCoViewers.SingleAsync()).UserId.Should().Be(_partnerId);

        var sharedState = await _context.SharedProfileMediaStates.SingleAsync();
        sharedState.IsCompleted.Should().BeTrue();
        sharedState.PlayCount.Should().Be(1);

        var partnerState = await _context.UserMediaStates.SingleAsync(s => s.UserId == _partnerId);
        partnerState.IsCompleted.Should().BeTrue();
        _cacheInvalidator.Received(1).InvalidateAll();
    }

    [Test]
    public async Task Handle_ShouldUntagSessionAndDropCoViewer_WhenAssigningSharedToPersonal()
    {
        var referenceId = AddSession(_hostId, _sharedProfileId, completed: true);
        _context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
        {
            ReferenceId = referenceId,
            UserId = _partnerId
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, null),
            CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync();
        session.SharedProfileId.Should().BeNull();
        session.SharedProfileNameSnapshot.Should().BeNull();
        (await _context.MediaPlaybackSessionCoViewers.CountAsync()).Should().Be(0);
        (await _context.SharedProfileMediaStates.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldMoveInProgressToSharedContinueWatching_WhenAssigningPersonal()
    {
        var referenceId = AddSession(_hostId, sharedProfileId: null, completed: false, position: 600, duration: 2000);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _hostId,
            MediaId = _movieId,
            IsCompleted = false
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, _sharedProfileId),
            CancellationToken.None);

        var personal = await _context.UserMediaStates.SingleAsync(s => s.UserId == _hostId);

        var shared = await _context.SharedProfileMediaStates.SingleAsync();
        shared.IsCompleted.Should().BeFalse();
        (await _context.UserMediaStates.CountAsync(s => s.UserId == _partnerId)).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldRestorePersonalContinueWatching_WhenUnassigningInProgress()
    {
        var referenceId = AddSession(_hostId, _sharedProfileId, completed: false, position: 800, duration: 2000);
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _movieId,
            IsCompleted = false
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, null),
            CancellationToken.None);

        (await _context.SharedProfileMediaStates.CountAsync()).Should().Be(0);
        var personal = await _context.UserMediaStates.SingleAsync(s => s.UserId == _hostId);
    }

    [Test]
    public async Task Handle_ShouldAllowPartnerToUnassignOwnSharedSession()
    {
        _currentUser.Id.Returns(_partnerId);
        var referenceId = AddSession(_partnerId, _sharedProfileId, completed: true);

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, null),
            CancellationToken.None);

        (await _context.MediaPlaybackSessions.SingleAsync()).SharedProfileId.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldForbidReassign_WhenCapabilityDisabled()
    {
        var capabilityOverride = await _context.UserCapabilityOverrides.SingleAsync(o =>
            o.UserId == _hostId && o.Capability == Capability.CanReassignHistory);
        capabilityOverride.Enabled = false;
        await _context.SaveChangesAsync();
        var referenceId = AddSession(_hostId, sharedProfileId: null, completed: true);

        var act = () => _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, _sharedProfileId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await _context.MediaPlaybackSessions.SingleAsync()).SharedProfileId.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldForbidAssigningSomeoneElsesPersonalSession()
    {
        var referenceId = AddSession(_partnerId, sharedProfileId: null, completed: true);

        var act = () => _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, _sharedProfileId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldAssignSomeoneElsesPersonalSession_WhenAdministrator()
    {
        _identityService.GetRolesAsync("host").Returns([Roles.Administrator]);
        var referenceId = AddSession(_partnerId, sharedProfileId: null, completed: true);

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, _sharedProfileId, AsAdministrator: true),
            CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync();
        session.SharedProfileId.Should().Be(_sharedProfileId);
        (await _context.MediaPlaybackSessionCoViewers.CountAsync(c => c.UserId == _hostId)).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldNoOp_WhenAlreadyAssignedToTarget()
    {
        var referenceId = AddSession(_hostId, _sharedProfileId, completed: true);

        await _handler.Handle(
            new ReassignPlaybackHistoryItemCommand(referenceId, _sharedProfileId),
            CancellationToken.None);

        _cacheInvalidator.DidNotReceive().InvalidateAll();
    }

    private Guid AddSession(
        Guid userId,
        Guid? sharedProfileId,
        bool completed,
        double position = 7200,
        double duration = 7200)
    {
        var referenceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MediaId = _movieId,
            SessionId = Guid.NewGuid(),
            ReferenceId = referenceId,
            SharedProfileId = sharedProfileId,
            SharedProfileNameSnapshot = sharedProfileId is null ? null : "Couple",
            StartedAt = now.AddMinutes(-20),
            LastUpdateAt = now,
            StoppedAt = now,
            CompletedAt = completed ? now : null,
            DurationSeconds = duration,
            PositionSeconds = position,
            WatchedDurationSeconds = position,
            State = PlaybackState.Ended
        });
        _context.SaveChanges();
        return referenceId;
    }

    private void GrantCapability(Guid userId, Capability capability)
    {
        _context.UserCapabilityOverrides.Add(new UserCapabilityOverride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Capability = capability,
            Enabled = true
        });
    }
}
