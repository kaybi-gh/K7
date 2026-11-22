using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Users.Queries.GetManagedAccounts;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Queries;

[TestFixture]
public class GetManagedAccountsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IIdentityService _identityService = null!;
    private IUser _currentUser = null!;
    private GetManagedAccountsQueryHandler _handler = null!;
    private Guid _guardianId;
    private readonly Dictionary<string, IReadOnlyList<string>> _roles = [];
    private readonly Dictionary<string, string?> _userNames = [];

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

        _roles.Clear();
        _userNames.Clear();
        _identityService = Substitute.For<IIdentityService>();
        _identityService.GetRolesAsync(Arg.Any<IEnumerable<string>>())
            .Returns(_ => (IReadOnlyDictionary<string, IReadOnlyList<string>>)_roles);
        _identityService.GetUserNamesAsync(Arg.Any<IEnumerable<string>>())
            .Returns(_ => (IReadOnlyDictionary<string, string?>)_userNames);

        _guardianId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _guardianId, IdentityUserId = "guardian", DisplayName = "guardian" });
        await _context.SaveChangesAsync();

        _currentUser = Substitute.For<IUser>();
        _currentUser.GetIdAsync(Arg.Any<CancellationToken>()).Returns(_guardianId);
        _handler = new GetManagedAccountsQueryHandler(_context, _currentUser, _identityService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnManagedAccounts_WhenCallerIsGuardian()
    {
        var zoe = await AddManagedAsync("zoe", "Zoe");
        var adam = await AddManagedAsync("adam", "Adam", pin: "1234");

        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.Accounts.Select(a => a.Id).Should().Equal(adam, zoe);
        result.Accounts[0].UserName.Should().Be("adam");
        result.Accounts[0].DisplayName.Should().Be("Adam");
        result.Accounts[0].HasPin.Should().BeTrue();
        result.Accounts[1].HasPin.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnNothing_WhenCallerManagesNobody()
    {
        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.Accounts.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReturnNothing_WhenCallerHasNoAccount()
    {
        await AddManagedAsync("zoe", "Zoe");
        _currentUser.GetIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.Accounts.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldNotReturnAccountsManagedBySomeoneElse()
    {
        var otherGuardian = Guid.NewGuid();
        _context.Users.Add(new User { Id = otherGuardian, IdentityUserId = "other", DisplayName = "other" });
        await _context.SaveChangesAsync();
        await AddManagedAsync("zoe", "Zoe", guardianId: otherGuardian);

        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.Accounts.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldSkipDeactivatedAccounts()
    {
        var zoe = await AddManagedAsync("zoe", "Zoe");
        var user = await _context.Users.SingleAsync(u => u.Id == zoe);
        user.IsActive = false;
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.Accounts.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldSkipAccountsThatBecameAdministrator()
    {
        await AddManagedAsync("zoe", "Zoe");
        _roles["zoe"] = [Roles.Administrator];

        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.Accounts.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReturnAvatarPictureIds_WhenAccountHasAvatar()
    {
        var zoe = await AddManagedAsync("zoe", "Zoe");
        var pictureId = Guid.NewGuid();
        _context.MetadataPictures.Add(new K7.Server.Domain.Entities.MetadataPicture
        {
            Id = pictureId,
            UserId = zoe,
            Type = K7.Server.Domain.Enums.MetadataPictureType.UserAvatar
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetManagedAccountsQuery(), CancellationToken.None);

        result.AvatarPictureIds.Should().Contain(zoe, pictureId);
    }

    private async Task<Guid> AddManagedAsync(string identityUserId, string displayName, string? pin = null, Guid? guardianId = null)
    {
        var id = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = id,
            IdentityUserId = identityUserId,
            DisplayName = displayName,
            PinHash = pin is null ? null : PinHashHelper.Hash(pin)
        });
        _context.UserGuardians.Add(new UserGuardian
        {
            Id = Guid.NewGuid(),
            UserId = id,
            GuardianUserId = guardianId ?? _guardianId
        });
        await _context.SaveChangesAsync();

        _roles[identityUserId] = [Roles.User];
        _userNames[identityUserId] = identityUserId;
        return id;
    }
}
