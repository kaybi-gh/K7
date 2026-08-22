using Ardalis.GuardClauses;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Stats.Commands.DeletePlaybackHistoryItem;
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
public class DeletePlaybackHistoryItemCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IIdentityService _identityService = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private IPlaybackProgressNotifier _notifier = null!;
    private DeletePlaybackHistoryItemCommandHandler _handler = null!;
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

        GrantCapability(_hostId, Capability.CanDeleteHistory);
        GrantCapability(_partnerId, Capability.CanDeleteHistory);
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_hostId);
        _identityService = Substitute.For<IIdentityService>();
        _identityService.GetRolesAsync("host").Returns([Roles.User]);
        _identityService.GetRolesAsync("partner").Returns([Roles.User]);
        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _notifier = Substitute.For<IPlaybackProgressNotifier>();
        _handler = new DeletePlaybackHistoryItemCommandHandler(
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
    public async Task Handle_ShouldRemoveSessionAndDecrementPlayCount_WhenDeletingPersonalCompleted()
    {
        var referenceId = AddSession(_hostId, sharedProfileId: null, completed: true);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _hostId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(referenceId), CancellationToken.None);

        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(0);
        var state = await _context.UserMediaStates.SingleAsync();
        state.PlayCount.Should().Be(0);
        state.IsCompleted.Should().BeFalse();
        _cacheInvalidator.Received(1).InvalidateAll();
    }

    [Test]
    public async Task Handle_ShouldDecrementSkipCount_WhenDeletingSkippedListen()
    {
        var referenceId = AddSession(
            _hostId,
            sharedProfileId: null,
            completed: false,
            position: 12,
            duration: 180);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _hostId,
            MediaId = _movieId,
            SkipCount = 2
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(referenceId), CancellationToken.None);

        (await _context.UserMediaStates.SingleAsync()).SkipCount.Should().Be(1);
        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldClearContinueWatching_WhenDeletingInProgress()
    {
        var referenceId = AddSession(
            _hostId,
            sharedProfileId: null,
            completed: false,
            position: 600,
            duration: 2000);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _hostId,
            MediaId = _movieId,
            IsCompleted = false
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(referenceId), CancellationToken.None);

        var state = await _context.UserMediaStates.SingleAsync();
    }

    [Test]
    public async Task Handle_ShouldTransferSessionAndKeepOthersStats_WhenActorOptsOutOfShared()
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
        _context.UserMediaStates.AddRange(
            new UserMediaState
            {
                UserId = _hostId,
                MediaId = _movieId,
                IsCompleted = true,
                PlayCount = 1,
            },
            new UserMediaState
            {
                UserId = _partnerId,
                MediaId = _movieId,
                IsCompleted = true,
                PlayCount = 1,
            });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(referenceId), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync();
        session.UserId.Should().Be(_partnerId);
        session.SharedProfileId.Should().Be(_sharedProfileId);
        (await _context.MediaPlaybackSessionCoViewers.CountAsync()).Should().Be(0);

        var shared = await _context.SharedProfileMediaStates.SingleAsync();
        shared.PlayCount.Should().Be(1);
        shared.IsCompleted.Should().BeTrue();

        var host = await _context.UserMediaStates.SingleAsync(s => s.UserId == _hostId);
        host.PlayCount.Should().Be(0);
        host.IsCompleted.Should().BeFalse();

        var partner = await _context.UserMediaStates.SingleAsync(s => s.UserId == _partnerId);
        partner.PlayCount.Should().Be(1);
        partner.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldKeepMemberCompleted_WhenTheyHaveAnotherCompletedSession()
    {
        var sharedReferenceId = AddSession(_hostId, _sharedProfileId, completed: true);
        AddSession(_partnerId, sharedProfileId: null, completed: true);
        _context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
        {
            ReferenceId = sharedReferenceId,
            UserId = _partnerId
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _partnerId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(sharedReferenceId), CancellationToken.None);

        var partner = await _context.UserMediaStates.SingleAsync(s => s.UserId == _partnerId);
        partner.PlayCount.Should().Be(1);
        partner.IsCompleted.Should().BeTrue();
        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(2);
    }

    [Test]
    public async Task Handle_ShouldAllowHostToDeleteMembersSharedPlay()
    {
        var referenceId = AddSession(_partnerId, _sharedProfileId, completed: true);
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1
        });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _partnerId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(referenceId), CancellationToken.None);

        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(0);
        (await _context.SharedProfileMediaStates.CountAsync()).Should().Be(0);
        var partner = await _context.UserMediaStates.SingleAsync(s => s.UserId == _partnerId);
        partner.PlayCount.Should().Be(1);
        partner.IsCompleted.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldForbidDeletingSomeoneElsesPersonalSession()
    {
        var referenceId = AddSession(_partnerId, sharedProfileId: null, completed: true);

        var act = () => _handler.Handle(
            new DeletePlaybackHistoryItemCommand(referenceId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldForbidDelete_WhenCapabilityDisabled()
    {
        var capabilityOverride = await _context.UserCapabilityOverrides.SingleAsync(o =>
            o.UserId == _hostId && o.Capability == Capability.CanDeleteHistory);
        capabilityOverride.Enabled = false;
        await _context.SaveChangesAsync();
        var referenceId = AddSession(_hostId, sharedProfileId: null, completed: true);

        var act = () => _handler.Handle(
            new DeletePlaybackHistoryItemCommand(referenceId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldDeleteSomeoneElsesPersonalSession_WhenAdminRemovesEntirePlay()
    {
        _identityService.GetRolesAsync("host").Returns([Roles.Administrator]);
        var referenceId = AddSession(_partnerId, sharedProfileId: null, completed: true);
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = _partnerId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new DeletePlaybackHistoryItemCommand(referenceId, RemoveEntirePlay: true),
            CancellationToken.None);

        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(0);
        var partner = await _context.UserMediaStates.SingleAsync();
        partner.PlayCount.Should().Be(0);
        partner.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldRemoveSharedRowWithoutUnwatchingMembers_WhenAdminRemovesEntirePlay()
    {
        _identityService.GetRolesAsync("host").Returns([Roles.Administrator]);
        var referenceId = AddSession(_partnerId, _sharedProfileId, completed: true);
        _context.MediaPlaybackSessionCoViewers.Add(new MediaPlaybackSessionCoViewer
        {
            ReferenceId = referenceId,
            UserId = _hostId
        });
        _context.SharedProfileMediaStates.Add(new SharedProfileMediaState
        {
            SharedProfileId = _sharedProfileId,
            MediaId = _movieId,
            IsCompleted = true,
            PlayCount = 1,
        });
        _context.UserMediaStates.AddRange(
            new UserMediaState
            {
                UserId = _hostId,
                MediaId = _movieId,
                IsCompleted = true,
                PlayCount = 1,
            },
            new UserMediaState
            {
                UserId = _partnerId,
                MediaId = _movieId,
                IsCompleted = true,
                PlayCount = 1,
            });
        await _context.SaveChangesAsync();

        await _handler.Handle(
            new DeletePlaybackHistoryItemCommand(referenceId, RemoveEntirePlay: true),
            CancellationToken.None);

        (await _context.MediaPlaybackSessions.CountAsync()).Should().Be(0);
        (await _context.SharedProfileMediaStates.CountAsync()).Should().Be(0);
        (await _context.UserMediaStates.SingleAsync(s => s.UserId == _hostId)).PlayCount.Should().Be(1);
        (await _context.UserMediaStates.SingleAsync(s => s.UserId == _partnerId)).PlayCount.Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldRemoveCoViewerOnly_WhenGuestOptsOutOfShared()
    {
        _currentUser.Id.Returns(_partnerId);
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
        _context.UserMediaStates.AddRange(
            new UserMediaState
            {
                UserId = _hostId,
                MediaId = _movieId,
                IsCompleted = true,
                PlayCount = 1,
            },
            new UserMediaState
            {
                UserId = _partnerId,
                MediaId = _movieId,
                IsCompleted = true,
                PlayCount = 1,
            });
        await _context.SaveChangesAsync();

        await _handler.Handle(new DeletePlaybackHistoryItemCommand(referenceId), CancellationToken.None);

        var session = await _context.MediaPlaybackSessions.SingleAsync();
        session.UserId.Should().Be(_hostId);
        session.SharedProfileId.Should().Be(_sharedProfileId);
        (await _context.MediaPlaybackSessionCoViewers.CountAsync()).Should().Be(0);

        var shared = await _context.SharedProfileMediaStates.SingleAsync();
        shared.PlayCount.Should().Be(1);

        (await _context.UserMediaStates.SingleAsync(s => s.UserId == _hostId)).PlayCount.Should().Be(1);
        var partner = await _context.UserMediaStates.SingleAsync(s => s.UserId == _partnerId);
        partner.PlayCount.Should().Be(0);
        partner.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenMissing()
    {
        var act = () => _handler.Handle(
            new DeletePlaybackHistoryItemCommand(Guid.NewGuid()),
            CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
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
