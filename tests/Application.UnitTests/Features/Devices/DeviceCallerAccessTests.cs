using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Devices;
using K7.Server.Application.Features.Devices.Queries.GetDevices;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Devices;

[TestFixture]
public class DeviceCallerAccessTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private User _owner = null!;
    private Device _ownDevice = null!;
    private Device _otherDevice = null!;

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

        _owner = new User { Id = Guid.NewGuid(), IdentityUserId = "owner-identity", DisplayName = "Owner" };
        var other = new User { Id = Guid.NewGuid(), IdentityUserId = "other-identity", DisplayName = "Other" };
        _ownDevice = NewDevice("Own");
        _otherDevice = NewDevice("Other");
        _ownDevice.Users.Add(_owner);
        _otherDevice.Users.Add(other);
        _context.Users.AddRange(_owner, other);
        _context.Devices.AddRange(_ownDevice, _otherDevice);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Test]
    public async Task EnsureCallerCanAccess_ShouldThrow_WhenCallerIsNotLinked()
    {
        var user = Caller(_owner.Id, isAdmin: false);
        var act = () => DeviceCallerAccess.EnsureCallerCanAccessAsync(
            _context, user, Identity(isAdmin: false), _otherDevice.Id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task EnsureCallerCanAccess_ShouldAllow_WhenCallerIsLinkedOrAdmin()
    {
        var owner = Caller(_owner.Id, isAdmin: false);
        var admin = Caller(null, isAdmin: true);

        await DeviceCallerAccess.EnsureCallerCanAccessAsync(
            _context, owner, Identity(isAdmin: false), _ownDevice.Id, CancellationToken.None);
        await DeviceCallerAccess.EnsureCallerCanAccessAsync(
            _context, admin, Identity(isAdmin: false), _otherDevice.Id, CancellationToken.None);
    }

    [Test]
    public async Task GetDevices_ShouldReturnOnlyCallerDevices_WhenCallerIsNotAdmin()
    {
        var handler = new GetDevicesQueryHandler(_context, Caller(_owner.Id, isAdmin: false), Identity(isAdmin: false));

        var page = await handler.Handle(new GetDevicesQuery { PageNumber = 1, PageSize = 20 }, CancellationToken.None);

        page.Items.Select(d => d.Id).Should().Equal(_ownDevice.Id);
    }

    [Test]
    public async Task GetDevices_ShouldReturnEveryDevice_WhenCallerIsAdmin()
    {
        var handler = new GetDevicesQueryHandler(_context, Caller(null, isAdmin: true), Identity(isAdmin: false));

        var page = await handler.Handle(new GetDevicesQuery { PageNumber = 1, PageSize = 20 }, CancellationToken.None);

        page.TotalCount.Should().Be(2);
    }

    private static Device NewDevice(string name) => new()
    {
        Id = Guid.NewGuid(),
        DeviceName = name,
        ClientType = ClientType.Web,
        LastSeen = DateTimeOffset.UtcNow
    };

    private static IUser Caller(Guid? userId, bool isAdmin)
    {
        var user = Substitute.For<IUser>();
        user.GetIdAsync(Arg.Any<CancellationToken>()).Returns(userId);
        user.IsInRole(Roles.Administrator).Returns(isAdmin);
        user.IdentityId.Returns(isAdmin ? "admin-identity" : "owner-identity");
        return user;
    }

    private static IIdentityService Identity(bool isAdmin)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsInRoleAsync(Arg.Any<string>(), Roles.Administrator).Returns(isAdmin);
        return identity;
    }
}
