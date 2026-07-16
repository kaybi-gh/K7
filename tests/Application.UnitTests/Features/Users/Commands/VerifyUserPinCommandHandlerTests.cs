using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Users.Commands.VerifyUserPin;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class VerifyUserPinCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private VerifyUserPinCommandHandler _handler = null!;

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
        _handler = new VerifyUserPinCommandHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnTrue_WhenUserHasNoPin()
    {
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, DisplayName = "nopin" });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new VerifyUserPinCommand(userId, "anything"), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldReturnTrue_WhenPinMatches()
    {
        var userId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = userId,
            DisplayName = "withpin",
            PinHash = PinHashHelper.Hash("4242")
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new VerifyUserPinCommand(userId, "4242"), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldReturnFalse_WhenPinDoesNotMatch()
    {
        var userId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = userId,
            DisplayName = "withpin",
            PinHash = PinHashHelper.Hash("4242")
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new VerifyUserPinCommand(userId, "0000"), CancellationToken.None);

        result.Should().BeFalse();
    }
}
