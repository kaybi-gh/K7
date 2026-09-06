using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfile;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles.Commands;

[TestFixture]
public class UpdateSharedProfileCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IIdentityService _identity = null!;
    private UpdateSharedProfileCommandHandler _handler = null!;
    private Guid _hostId;
    private Guid _memberId;
    private Guid _groupId;

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

        _hostId = Guid.NewGuid();
        _memberId = Guid.NewGuid();
        _groupId = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = _hostId, DisplayName = "host" },
            new User { Id = _memberId, DisplayName = "member" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _groupId,
            Name = "Family",
            HostUserId = _hostId,
            CreatedByUserId = _hostId,
            Members =
            [
                new SharedProfileMember { Id = Guid.NewGuid(), SharedProfileId = _groupId, UserId = _hostId },
                new SharedProfileMember { Id = Guid.NewGuid(), SharedProfileId = _groupId, UserId = _memberId }
            ]
        });
        await _context.SaveChangesAsync();

        _currentUser = Substitute.For<IUser>();
        _identity = Substitute.For<IIdentityService>();
        _identity.IsInRoleAsync(Arg.Any<string>(), Roles.Administrator).Returns(false);
        _handler = new UpdateSharedProfileCommandHandler(_context, _currentUser, _identity);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldForbidMember_WhenReassigningHost()
    {
        _currentUser.Id.Returns(_memberId);
        _currentUser.IdentityId.Returns("member");

        var act = () => _handler.Handle(new UpdateSharedProfileCommand
        {
            Id = _groupId,
            HostUserId = _memberId
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        (await _context.SharedProfiles.SingleAsync()).HostUserId.Should().Be(_hostId);
    }

    [Test]
    public async Task Handle_ShouldAllowHost_WhenReassigningHost()
    {
        _currentUser.Id.Returns(_hostId);
        _currentUser.IdentityId.Returns("host");

        await _handler.Handle(new UpdateSharedProfileCommand
        {
            Id = _groupId,
            HostUserId = _memberId
        }, CancellationToken.None);

        (await _context.SharedProfiles.SingleAsync()).HostUserId.Should().Be(_memberId);
    }
}
