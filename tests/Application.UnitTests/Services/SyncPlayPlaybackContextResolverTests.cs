using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class SyncPlayPlaybackContextResolverTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISyncPlayCoordinator _coordinator = null!;
    private SyncPlayPlaybackContextResolver _resolver = null!;

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

        _coordinator = Substitute.For<ISyncPlayCoordinator>();
        _resolver = new SyncPlayPlaybackContextResolver(_coordinator, _context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenGroupMissing()
    {
        _coordinator.GetGroup(Arg.Any<Guid>()).Returns((SyncPlayGroupInfo?)null);

        var result = await _resolver.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "user-1");

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenCallerNotMember()
    {
        var group = CreateGroup("other");
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = "other",
            DeviceId = Guid.NewGuid(),
            DisplayName = "Other",
            DeviceName = "TV"
        };
        _coordinator.GetGroup(group.GroupId).Returns(group);

        var result = await _resolver.ResolveAsync(group.GroupId, Guid.NewGuid(), "user-1");

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldReturnNull_WhenAloneInGroup()
    {
        var identity = "user-1";
        var group = CreateGroup(identity);
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = identity,
            DeviceId = Guid.NewGuid(),
            DisplayName = "Kay",
            DeviceName = "TV"
        };
        _coordinator.GetGroup(group.GroupId).Returns(group);

        var result = await _resolver.ResolveAsync(group.GroupId, Guid.NewGuid(), identity);

        result.Should().BeNull();
    }

    [Test]
    public async Task ResolveAsync_ShouldBuildSnapshotAndCoViewerIds()
    {
        var currentUserId = Guid.NewGuid();
        var coViewerId = Guid.NewGuid();
        var identity = "user-1";
        var otherIdentity = "user-2";

        _context.Users.AddRange(
            new User { Id = currentUserId, DisplayName = "Kay", IdentityUserId = identity, Role = "User" },
            new User { Id = coViewerId, DisplayName = "Ada", IdentityUserId = otherIdentity, Role = "User" });
        await _context.SaveChangesAsync();

        var group = CreateGroup(identity, SyncPlayGroupState.Playing);
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = identity,
            DeviceId = Guid.NewGuid(),
            DisplayName = "Kay",
            DeviceName = "TV"
        };
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = otherIdentity,
            DeviceId = Guid.NewGuid(),
            DisplayName = "Ada",
            DeviceName = "Phone"
        };
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = null,
            DeviceId = Guid.NewGuid(),
            DisplayName = "Guest",
            DeviceName = "Tablet",
            IsGuest = true
        };
        _coordinator.GetGroup(group.GroupId).Returns(group);

        var result = await _resolver.ResolveAsync(group.GroupId, currentUserId, identity);

        result.Should().NotBeNull();
        result!.CoWatchingWithSnapshot.Split(", ").Should().BeEquivalentTo("Ada", "Guest");
        result.CoViewerUserIds.Should().Equal(coViewerId);
    }

    [Test]
    public async Task ResolveAsync_ShouldLookupIdentity_WhenNotProvided()
    {
        var currentUserId = Guid.NewGuid();
        var identity = "user-1";
        _context.Users.Add(new User
        {
            Id = currentUserId,
            DisplayName = "Kay",
            IdentityUserId = identity,
            Role = "User"
        });
        await _context.SaveChangesAsync();

        var group = CreateGroup(identity);
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = identity,
            DeviceId = Guid.NewGuid(),
            DisplayName = "Kay",
            DeviceName = "TV"
        };
        group.Members[Guid.NewGuid()] = new SyncPlayMember
        {
            IdentityUserId = "user-2",
            DeviceId = Guid.NewGuid(),
            DisplayName = "Ada",
            DeviceName = "Phone"
        };
        _coordinator.GetGroup(group.GroupId).Returns(group);

        var result = await _resolver.ResolveAsync(group.GroupId, currentUserId, currentIdentityUserId: null);

        result.Should().NotBeNull();
        result!.CoWatchingWithSnapshot.Should().Be("Ada");
    }

    private static SyncPlayGroupInfo CreateGroup(string creatorUserId, SyncPlayGroupState state = SyncPlayGroupState.Idle) =>
        new()
        {
            GroupId = Guid.NewGuid(),
            GroupName = "Watch party",
            CreatorUserId = creatorUserId,
            State = state
        };
}
