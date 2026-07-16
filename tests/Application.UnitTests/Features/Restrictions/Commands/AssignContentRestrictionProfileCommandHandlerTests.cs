using Ardalis.GuardClauses;
using K7.Server.Application.Features.Restrictions.Commands.AssignContentRestrictionProfile;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Restrictions.Commands;

[TestFixture]
public class AssignContentRestrictionProfileCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private AssignContentRestrictionProfileCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _profileId;

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

        _userId = Guid.NewGuid();
        _profileId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "kid" });
        _context.ContentRestrictionProfiles.Add(new ContentRestrictionProfile
        {
            Id = _profileId,
            Name = "Kids"
        });
        _context.SaveChanges();

        _handler = new AssignContentRestrictionProfileCommandHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldAssignProfile()
    {
        await _handler.Handle(new AssignContentRestrictionProfileCommand
        {
            UserId = _userId,
            ProfileId = _profileId
        }, CancellationToken.None);

        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.ContentRestrictionProfileId.Should().Be(_profileId);
    }

    [Test]
    public async Task Handle_ShouldClearProfile_WhenProfileIdNull()
    {
        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.ContentRestrictionProfileId = _profileId;
        await _context.SaveChangesAsync();

        await _handler.Handle(new AssignContentRestrictionProfileCommand
        {
            UserId = _userId,
            ProfileId = null
        }, CancellationToken.None);

        user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.ContentRestrictionProfileId.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenUserMissing()
    {
        var act = () => _handler.Handle(new AssignContentRestrictionProfileCommand
        {
            UserId = Guid.NewGuid(),
            ProfileId = _profileId
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenProfileMissing()
    {
        var act = () => _handler.Handle(new AssignContentRestrictionProfileCommand
        {
            UserId = _userId,
            ProfileId = Guid.NewGuid()
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
