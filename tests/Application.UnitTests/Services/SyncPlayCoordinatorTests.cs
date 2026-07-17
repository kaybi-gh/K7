using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class SyncPlayCoordinatorTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;
    private SyncPlayCoordinator _coordinator = null!;
    private string _creatorId = null!;
    private Guid _creatorDeviceId;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ISyncPlayInviteStore, SyncPlayInviteStore>();
        _serviceProvider = services.BuildServiceProvider();

        using (var scope = _serviceProvider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        }

        _coordinator = new SyncPlayCoordinator(
            Substitute.For<ILogger<SyncPlayCoordinator>>(),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());
        _creatorId = "user-1";
        _creatorDeviceId = Guid.NewGuid();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    [Test]
    public void CreateGroup_ShouldRegisterCreatorAsMember()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        group.CreatorUserId.Should().Be(_creatorId);
        group.Members.Should().ContainKey(_creatorDeviceId);
        group.State.Should().Be(SyncPlayGroupState.Idle);
        _coordinator.GetGroup(group.GroupId).Should().NotBeNull();
    }

    [Test]
    public void JoinAndLeave_ShouldDestroyGroup_WhenLastMemberLeaves()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");
        var guestDevice = Guid.NewGuid();

        _coordinator.JoinGroup(group.GroupId, null, guestDevice, "Guest", "Phone", isGuest: true).Should().NotBeNull();
        group.Members.Should().HaveCount(2);

        var leaveGuest = _coordinator.LeaveGroup(group.GroupId, guestDevice);
        leaveGuest.GroupDestroyed.Should().BeFalse();

        var leaveCreator = _coordinator.LeaveGroup(group.GroupId, _creatorDeviceId);
        leaveCreator.GroupDestroyed.Should().BeTrue();
        _coordinator.GetGroup(group.GroupId).Should().BeNull();
    }

    [Test]
    public void IssueCommand_ShouldPermitPlayPauseSeekForMembers()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        var play = _coordinator.IssueCommand(group.GroupId, _creatorDeviceId, SyncPlayCommandType.Play, null);
        play.Permitted.Should().BeTrue();
        play.NewState.Should().Be(SyncPlayGroupState.Playing);

        var pause = _coordinator.IssueCommand(group.GroupId, _creatorDeviceId, SyncPlayCommandType.Pause, 12);
        pause.Permitted.Should().BeTrue();
        pause.NewState.Should().Be(SyncPlayGroupState.Paused);
        group.Position.Should().Be(12);

        var seek = _coordinator.IssueCommand(group.GroupId, _creatorDeviceId, SyncPlayCommandType.SeekTo, 40);
        seek.Permitted.Should().BeTrue();
        seek.NewState.Should().Be(SyncPlayGroupState.WaitingForReady);
        seek.SeekPosition.Should().Be(40);
    }

    [Test]
    public void IssueCommand_ShouldDenyUnknownMember()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        var result = _coordinator.IssueCommand(group.GroupId, Guid.NewGuid(), SyncPlayCommandType.Play, null);

        result.Permitted.Should().BeFalse();
    }

    [Test]
    public void ReportReady_ShouldStartPlayback_WhenAllMembersReady()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");
        var otherDevice = Guid.NewGuid();
        _coordinator.JoinGroup(group.GroupId, "user-2", otherDevice, "Marie", "Laptop", isGuest: false);
        _coordinator.IssueCommand(group.GroupId, _creatorDeviceId, SyncPlayCommandType.SeekTo, 0);

        var first = _coordinator.ReportReady(group.GroupId, _creatorDeviceId);
        first.AllReady.Should().BeFalse();

        var second = _coordinator.ReportReady(group.GroupId, otherDevice);
        second.AllReady.Should().BeTrue();
        second.CatchUpOnly.Should().BeFalse();
        group.State.Should().Be(SyncPlayGroupState.Playing);
    }

    [Test]
    public void QueueNavigation_ShouldMoveCurrentMedia()
    {
        var first = new SyncPlayQueueItemDto
        {
            QueueItemId = Guid.NewGuid(),
            MediaReferenceId = Guid.NewGuid(),
            Title = "One",
            Duration = 100
        };
        var second = new SyncPlayQueueItemDto
        {
            QueueItemId = Guid.NewGuid(),
            MediaReferenceId = Guid.NewGuid(),
            Title = "Two",
            Duration = 200
        };

        var group = _coordinator.CreateGroup(
            _creatorId,
            _creatorDeviceId,
            "Kay",
            "TV",
            initialMedia: first);

        _coordinator.AddToQueue(group.GroupId, _creatorDeviceId, second).Should().BeTrue();

        var next = _coordinator.NavigateQueue(group.GroupId, _creatorDeviceId, forward: true);
        next.Should().NotBeNull();
        next!.Title.Should().Be("Two");
        group.CurrentMedia!.Title.Should().Be("Two");
        group.State.Should().Be(SyncPlayGroupState.WaitingForReady);
    }

    [Test]
    public void GuestToken_ShouldValidateAndReject_WhenWrongOrExpired()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        var guestToken = _coordinator.GenerateGuestToken(group.GroupId, _creatorId);
        guestToken.Should().NotBeNullOrEmpty();
        _coordinator.ValidateGuestToken(group.GroupId, guestToken!).Should().BeTrue();
        _coordinator.ValidateGuestToken(group.GroupId, "bad").Should().BeFalse();

        group.GuestTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        _coordinator.ValidateGuestToken(group.GroupId, guestToken!).Should().BeFalse();
    }

    [Test]
    public async Task InviteToken_ShouldResolveToGroup_WhileGroupIsActive()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        var invite = await _coordinator.GenerateInviteTokenAsync(group.GroupId, _creatorId);
        invite.Should().NotBeNullOrEmpty();

        (await _coordinator.ResolveInviteTokenAsync(invite!)).Should().Be(group.GroupId);
    }

    [Test]
    public async Task InviteToken_ShouldSurviveRestart_ButNotResolveWithoutActiveGroup()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");
        var invite = await _coordinator.GenerateInviteTokenAsync(group.GroupId, _creatorId);
        invite.Should().NotBeNullOrEmpty();

        // Simulate a restart: fresh coordinator, same durable store, in-memory groups gone.
        var restarted = new SyncPlayCoordinator(
            Substitute.For<ILogger<SyncPlayCoordinator>>(),
            _serviceProvider.GetRequiredService<IServiceScopeFactory>());

        using (var scope = _serviceProvider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ISyncPlayInviteStore>();
            (await store.ResolveGroupIdAsync(invite!)).Should().Be(group.GroupId);
        }

        (await restarted.ResolveInviteTokenAsync(invite!)).Should().BeNull();
    }

    [Test]
    public void ChatRateLimit_ShouldThrottleRapidMessages()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        _coordinator.TryConsumeChatRateLimit(group.GroupId, _creatorDeviceId).Should().BeTrue();
        _coordinator.TryConsumeChatRateLimit(group.GroupId, _creatorDeviceId).Should().BeFalse();
    }

    [Test]
    public void ReactionRateLimit_ShouldThrottleRapidReactions()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        _coordinator.TryConsumeReactionRateLimit(group.GroupId, _creatorDeviceId).Should().BeTrue();
        _coordinator.TryConsumeReactionRateLimit(group.GroupId, _creatorDeviceId).Should().BeFalse();
    }

    [Test]
    public void ReportPosition_ShouldReturnCorrection_WhenDriftExceedsThreshold()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");
        _coordinator.IssueCommand(group.GroupId, _creatorDeviceId, SyncPlayCommandType.Play, null);

        // Simulate the group having been playing for a while so the expected position has advanced.
        group.PlayStartedAtUtc = DateTime.UtcNow.AddSeconds(-10);
        group.PlayStartedAtPosition = 0;

        var noDrift = _coordinator.ReportPosition(group.GroupId, _creatorDeviceId, 10.2);
        noDrift.Should().BeNull();

        var drifted = _coordinator.ReportPosition(group.GroupId, _creatorDeviceId, 2);
        drifted.Should().NotBeNull();
        drifted!.Value.DeviceId.Should().Be(_creatorDeviceId);
        drifted.Value.Position.Should().BeApproximately(10, 1);
    }

    [Test]
    public void ReportPosition_ShouldReturnNull_WhenNotPlaying()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        var result = _coordinator.ReportPosition(group.GroupId, _creatorDeviceId, 999);

        result.Should().BeNull();
    }

    [Test]
    public void Kick_ShouldOnlyAllowCreator()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");
        var target = Guid.NewGuid();
        _coordinator.JoinGroup(group.GroupId, "user-2", target, "Marie", "Laptop", isGuest: false);

        _coordinator.Kick(group.GroupId, "user-2", target).Should().BeNull();
        var kicked = _coordinator.Kick(group.GroupId, _creatorId, target);
        kicked.Should().NotBeNull();
        kicked!.TargetDeviceId.Should().Be(target);
        group.Members.Should().NotContainKey(target);
    }
}
