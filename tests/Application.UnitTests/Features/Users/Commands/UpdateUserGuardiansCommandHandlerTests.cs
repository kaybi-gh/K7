using Ardalis.GuardClauses;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.UpdateUserGuardians;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class UpdateUserGuardiansCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IIdentityService _identityService = null!;
    private UpdateUserGuardiansCommandHandler _handler = null!;

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
        _handler = new UpdateUserGuardiansCommandHandler(_context, _identityService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldLinkGuardians_WhenAccountIsAnOrdinaryUser()
    {
        var child = await AddUserAsync("child", Roles.User);
        var father = await AddUserAsync("father", Roles.Administrator);
        var mother = await AddUserAsync("mother", Roles.User);

        await _handler.Handle(Command(child, father, mother), CancellationToken.None);

        var guardians = await GuardiansOfAsync(child);
        guardians.Should().BeEquivalentTo([father, mother]);
    }

    [Test]
    public async Task Handle_ShouldReplaceGuardians_WhenListChanges()
    {
        var child = await AddUserAsync("child", Roles.User);
        var father = await AddUserAsync("father", Roles.User);
        var mother = await AddUserAsync("mother", Roles.User);
        var grandma = await AddUserAsync("grandma", Roles.User);
        await _handler.Handle(Command(child, father, mother), CancellationToken.None);
        var keptLinkId = await _context.UserGuardians
            .Where(g => g.UserId == child && g.GuardianUserId == mother)
            .Select(g => g.Id)
            .SingleAsync();

        await _handler.Handle(Command(child, mother, grandma), CancellationToken.None);

        var guardians = await GuardiansOfAsync(child);
        guardians.Should().BeEquivalentTo([mother, grandma]);
        var linkId = await _context.UserGuardians
            .Where(g => g.UserId == child && g.GuardianUserId == mother)
            .Select(g => g.Id)
            .SingleAsync();
        linkId.Should().Be(keptLinkId);
    }

    [Test]
    public async Task Handle_ShouldRemoveAllGuardians_WhenListIsEmpty()
    {
        var child = await AddUserAsync("child", Roles.User);
        var father = await AddUserAsync("father", Roles.User);
        await _handler.Handle(Command(child, father), CancellationToken.None);

        await _handler.Handle(Command(child), CancellationToken.None);

        (await GuardiansOfAsync(child)).Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldNotTouchOtherAccounts_WhenGuardiansChange()
    {
        var child = await AddUserAsync("child", Roles.User);
        var sibling = await AddUserAsync("sibling", Roles.User);
        var father = await AddUserAsync("father", Roles.User);
        await _handler.Handle(Command(child, father), CancellationToken.None);
        await _handler.Handle(Command(sibling, father), CancellationToken.None);

        await _handler.Handle(Command(child), CancellationToken.None);

        (await GuardiansOfAsync(sibling)).Should().BeEquivalentTo([father]);
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenAccountDoesNotExist()
    {
        var father = await AddUserAsync("father", Roles.User);

        var act = () => _handler.Handle(Command(Guid.NewGuid(), father), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenAccountIsAdministrator()
    {
        var admin = await AddUserAsync("admin", Roles.Administrator);
        var father = await AddUserAsync("father", Roles.User);

        var act = () => _handler.Handle(Command(admin, father), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        (await GuardiansOfAsync(admin)).Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenAccountIsGuest()
    {
        var guest = await AddUserAsync("guest", Roles.Guest);
        var father = await AddUserAsync("father", Roles.User);

        var act = () => _handler.Handle(Command(guest, father), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenAccountHasNoIdentity()
    {
        var profile = Guid.NewGuid();
        _context.Users.Add(new User { Id = profile, DisplayName = "profile" });
        await _context.SaveChangesAsync();
        var father = await AddUserAsync("father", Roles.User);

        var act = () => _handler.Handle(Command(profile, father), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenAccountIsVirtual()
    {
        var remote = await AddVirtualUserAsync("remote");
        var father = await AddUserAsync("father", Roles.User);

        var act = () => _handler.Handle(Command(remote, father), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenGuardianDoesNotExist()
    {
        var child = await AddUserAsync("child", Roles.User);

        var act = () => _handler.Handle(Command(child, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        (await GuardiansOfAsync(child)).Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenGuardianIsGuest()
    {
        var child = await AddUserAsync("child", Roles.User);
        var guest = await AddUserAsync("guest", Roles.Guest);

        var act = () => _handler.Handle(Command(child, guest), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenGuardianIsVirtual()
    {
        var child = await AddUserAsync("child", Roles.User);
        var remote = await AddVirtualUserAsync("remote");

        var act = () => _handler.Handle(Command(child, remote), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldStillClearGuardians_WhenAccountCanNoLongerBeManaged()
    {
        var child = await AddUserAsync("child", Roles.User);
        var father = await AddUserAsync("father", Roles.User);
        await _handler.Handle(Command(child, father), CancellationToken.None);
        _identityService.GetRolesAsync("child").Returns([Roles.Administrator]);

        await _handler.Handle(Command(child), CancellationToken.None);

        (await GuardiansOfAsync(child)).Should().BeEmpty();
    }

    private static UpdateUserGuardiansCommand Command(Guid id, params Guid[] guardianUserIds) =>
        new() { Id = id, GuardianUserIds = guardianUserIds };

    private async Task<Guid> AddUserAsync(string identityUserId, string role)
    {
        var id = Guid.NewGuid();
        _context.Users.Add(new User { Id = id, IdentityUserId = identityUserId, DisplayName = identityUserId });
        await _context.SaveChangesAsync();
        _identityService.GetRolesAsync(identityUserId).Returns([role]);
        return id;
    }

    private async Task<Guid> AddVirtualUserAsync(string identityUserId)
    {
        var peerId = Guid.NewGuid();
        _context.PeerServers.Add(new PeerServer { Id = peerId, Name = "peer", BaseUrl = "https://peer.example" });
        var id = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = id,
            IdentityUserId = identityUserId,
            DisplayName = identityUserId,
            PeerServerId = peerId
        });
        await _context.SaveChangesAsync();
        _identityService.GetRolesAsync(identityUserId).Returns([Roles.User]);
        return id;
    }

    private async Task<List<Guid>> GuardiansOfAsync(Guid userId) =>
        await _context.UserGuardians
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .Select(g => g.GuardianUserId)
            .ToListAsync();
}
