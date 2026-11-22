using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Users.Queries.GetGuardianSessionTarget;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Queries;

[TestFixture]
public class GetGuardianSessionTargetQueryHandlerTests
{
    private const string GuardianIdentity = "guardian";
    private const string ChildIdentity = "child";

    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IIdentityService _identityService = null!;
    private GetGuardianSessionTargetQueryHandler _handler = null!;
    private User _guardian = null!;
    private User _child = null!;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _identityService = Substitute.For<IIdentityService>();
        _identityService.GetRolesAsync(GuardianIdentity).Returns([Roles.User]);
        _identityService.GetRolesAsync(ChildIdentity).Returns([Roles.User]);
        _handler = new GetGuardianSessionTargetQueryHandler(_context, _identityService);

        _guardian = new User { Id = Guid.NewGuid(), IdentityUserId = GuardianIdentity, DisplayName = "guardian" };
        _child = new User { Id = Guid.NewGuid(), IdentityUserId = ChildIdentity, DisplayName = "child" };
        _context.Users.AddRange(_guardian, _child);
        _context.UserGuardians.Add(new UserGuardian
        {
            Id = Guid.NewGuid(),
            UserId = _child.Id,
            GuardianUserId = _guardian.Id
        });
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnManagedAccount_WhenCallerIsItsGuardian()
    {
        var target = await _handler.Handle(Query(), CancellationToken.None);

        target.Should().Be(new GuardianSessionTarget(_guardian.Id, _child.Id, ChildIdentity, HasPin: false));
    }

    [Test]
    public async Task Handle_ShouldReportPin_WhenManagedAccountHasOne()
    {
        _child.PinHash = PinHashHelper.Hash("1234");
        await _context.SaveChangesAsync();

        var target = await _handler.Handle(Query(), CancellationToken.None);

        target.HasPin.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenCallerIsNotGuardianOfThatAccount()
    {
        var stranger = new User { Id = Guid.NewGuid(), IdentityUserId = "stranger", DisplayName = "stranger" };
        _context.Users.Add(stranger);
        await _context.SaveChangesAsync();

        var act = () => _handler.Handle(
            new GetGuardianSessionTargetQuery("stranger", _child.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenLinkGoesTheOtherWay()
    {
        var act = () => _handler.Handle(
            new GetGuardianSessionTargetQuery(ChildIdentity, _guardian.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenGuardianIsUnknown()
    {
        var act = () => _handler.Handle(
            new GetGuardianSessionTargetQuery("nobody", _child.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenGuardianIsDeactivated()
    {
        _guardian.IsActive = false;
        await _context.SaveChangesAsync();

        var act = () => _handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenManagedAccountIsDeactivated()
    {
        _child.IsActive = false;
        await _context.SaveChangesAsync();

        var act = () => _handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenManagedAccountBecameAdministrator()
    {
        _identityService.GetRolesAsync(ChildIdentity).Returns([Roles.Administrator]);

        var act = () => _handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldThrowForbidden_WhenManagedAccountIsNoLongerAUser()
    {
        _identityService.GetRolesAsync(ChildIdentity).Returns([Roles.Guest]);

        var act = () => _handler.Handle(Query(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    private GetGuardianSessionTargetQuery Query() => new(GuardianIdentity, _child.Id);
}
