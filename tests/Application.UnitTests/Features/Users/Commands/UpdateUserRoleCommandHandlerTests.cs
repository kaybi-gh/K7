using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.UpdateUserRole;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class UpdateUserRoleCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IIdentityService _identityService = null!;
    private UpdateUserRoleCommandHandler _handler = null!;
    private Guid _childId;
    private Guid _guardianId;

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
        _handler = new UpdateUserRoleCommandHandler(_context, _identityService);

        _childId = Guid.NewGuid();
        _guardianId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _childId, IdentityUserId = "child", DisplayName = "child" });
        _context.Users.Add(new User { Id = _guardianId, IdentityUserId = "guardian", DisplayName = "guardian" });
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenPromotingManagedAccountToAdministrator()
    {
        await AddGuardianLinkAsync();

        var act = () => _handler.Handle(
            new UpdateUserRoleCommand { Id = _childId, Role = Roles.Administrator },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _identityService.DidNotReceive().SetRoleAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Handle_ShouldSetRole_WhenManagedAccountStaysUser()
    {
        await AddGuardianLinkAsync();

        await _handler.Handle(
            new UpdateUserRoleCommand { Id = _childId, Role = Roles.User },
            CancellationToken.None);

        await _identityService.Received(1).SetRoleAsync("child", Roles.User);
    }

    [Test]
    public async Task Handle_ShouldSetAdministrator_WhenAccountHasNoGuardian()
    {
        await _handler.Handle(
            new UpdateUserRoleCommand { Id = _childId, Role = Roles.Administrator },
            CancellationToken.None);

        await _identityService.Received(1).SetRoleAsync("child", Roles.Administrator);
    }

    [Test]
    public async Task Handle_ShouldSetAdministrator_WhenAccountIsOnlyAGuardian()
    {
        await AddGuardianLinkAsync();

        await _handler.Handle(
            new UpdateUserRoleCommand { Id = _guardianId, Role = Roles.Administrator },
            CancellationToken.None);

        await _identityService.Received(1).SetRoleAsync("guardian", Roles.Administrator);
    }

    private async Task AddGuardianLinkAsync()
    {
        _context.UserGuardians.Add(new UserGuardian
        {
            Id = Guid.NewGuid(),
            UserId = _childId,
            GuardianUserId = _guardianId
        });
        await _context.SaveChangesAsync();
    }
}
