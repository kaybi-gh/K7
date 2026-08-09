using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles.Commands.VerifySharedProfilePin;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles.Commands;

[TestFixture]
public class VerifySharedProfilePinCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private VerifySharedProfilePinCommandHandler _handler = null!;

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
        _handler = new VerifySharedProfilePinCommandHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnFalse_WhenSharedProfileDoesNotExist()
    {
        var result = await _handler.Handle(
            new VerifySharedProfilePinCommand(Guid.NewGuid(), "1234"),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnTrue_WhenSharedProfileHasNoPin()
    {
        var profileId = await SeedProfileAsync(pinHash: null);

        var result = await _handler.Handle(
            new VerifySharedProfilePinCommand(profileId, "anything"),
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldReturnTrue_WhenPinMatches()
    {
        var profileId = await SeedProfileAsync(PinHashHelper.Hash("4242"));

        var result = await _handler.Handle(
            new VerifySharedProfilePinCommand(profileId, "4242"),
            CancellationToken.None);

        result.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldReturnFalse_WhenPinDoesNotMatch()
    {
        var profileId = await SeedProfileAsync(PinHashHelper.Hash("4242"));

        var result = await _handler.Handle(
            new VerifySharedProfilePinCommand(profileId, "0000"),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    private async Task<Guid> SeedProfileAsync(string? pinHash)
    {
        var hostId = Guid.NewGuid();
        _context.Users.Add(new User { Id = hostId, DisplayName = "host" });

        var profileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = profileId,
            Name = "Couple",
            HostUserId = hostId,
            CreatedByUserId = hostId,
            PinHash = pinHash
        });
        await _context.SaveChangesAsync();
        return profileId;
    }
}
