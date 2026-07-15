using Ardalis.GuardClauses;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.DeleteUser;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class DeleteUserCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IIdentityService _identityService = null!;
    private IUser _currentUser = null!;
    private DeleteUserCommandHandler _handler = null!;

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

        _identityService = Substitute.For<IIdentityService>();
        _currentUser = Substitute.For<IUser>();
        _handler = new DeleteUserCommandHandler(_context, _identityService, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenDeletingOwnAccount()
    {
        var identityId = "self";
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = identityId, DisplayName = "me" });
        await _context.SaveChangesAsync();

        _currentUser.IdentityId.Returns(identityId);

        var act = () => _handler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _identityService.DidNotReceive().DeleteUserAsync(Arg.Any<string>());
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenDeletingGuest()
    {
        var identityId = "guest-id";
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = identityId, DisplayName = "guest" });
        await _context.SaveChangesAsync();

        _currentUser.IdentityId.Returns("admin");
        _identityService.GetRolesAsync(identityId).Returns([Roles.Guest]);

        var act = () => _handler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        await _identityService.DidNotReceive().DeleteUserAsync(Arg.Any<string>());
    }

    [Test]
    public async Task Handle_ShouldRemoveDomainUserAndIdentity()
    {
        var identityId = "user-id";
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = identityId, DisplayName = "bob" });
        await _context.SaveChangesAsync();

        _currentUser.IdentityId.Returns("admin");
        _identityService.GetRolesAsync(identityId).Returns([Roles.User]);

        await _handler.Handle(new DeleteUserCommand(userId), CancellationToken.None);

        (await _context.Users.FindAsync(userId)).Should().BeNull();
        await _identityService.Received(1).DeleteUserAsync(identityId);
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenUserMissing()
    {
        var act = () => _handler.Handle(new DeleteUserCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
