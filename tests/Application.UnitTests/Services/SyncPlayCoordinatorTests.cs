using K7.Server.Application.Services;
using K7.Shared.Dtos;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class SyncPlayCoordinatorTests
{
    private SyncPlayCoordinator _coordinator = null!;
    private string _creatorId = null!;
    private Guid _creatorDeviceId;

    [SetUp]
    public void SetUp()
    {
        _coordinator = new SyncPlayCoordinator(Substitute.For<ILogger<SyncPlayCoordinator>>());
        _creatorId = "user-1";
        _creatorDeviceId = Guid.NewGuid();
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
    public void GuestAndInviteTokens_ShouldValidate()
    {
        var group = _coordinator.CreateGroup(_creatorId, _creatorDeviceId, "Kay", "TV");

        var guestToken = _coordinator.GenerateGuestToken(group.GroupId, _creatorId);
        guestToken.Should().NotBeNullOrEmpty();
        _coordinator.ValidateGuestToken(group.GroupId, guestToken!).Should().BeTrue();
        _coordinator.ValidateGuestToken(group.GroupId, "bad").Should().BeFalse();

        var invite = _coordinator.GenerateInviteToken(group.GroupId, _creatorId);
        invite.Should().NotBeNullOrEmpty();
        _coordinator.ResolveInviteToken(invite!).Should().Be(group.GroupId);
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
