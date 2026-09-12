using Ardalis.GuardClauses;
using K7.Server.Application.Features.Restrictions.Commands.UpdateUserAgeRestriction;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Restrictions.Commands;

[TestFixture]
public class UpdateUserAgeRestrictionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private UpdateUserAgeRestrictionCommandHandler _handler = null!;
    private Guid _userId;

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
        _context.Users.Add(new User { Id = _userId, DisplayName = "kid" });
        _context.SaveChanges();

        _handler = new UpdateUserAgeRestrictionCommandHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldStoreDateOfBirthWithoutEnabling_WhenDisabled()
    {
        await _handler.Handle(new UpdateUserAgeRestrictionCommand
        {
            UserId = _userId,
            Enabled = false,
            DateOfBirth = new DateOnly(1996, 4, 1)
        }, CancellationToken.None);

        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.AgeRestrictionEnabled.Should().BeFalse();
        user.DateOfBirth.Should().Be(new DateOnly(1996, 4, 1));
    }

    [Test]
    public async Task Handle_ShouldEnableAgeRestriction()
    {
        await _handler.Handle(new UpdateUserAgeRestrictionCommand
        {
            UserId = _userId,
            Enabled = true,
            DateOfBirth = new DateOnly(2014, 9, 10)
        }, CancellationToken.None);

        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.AgeRestrictionEnabled.Should().BeTrue();
        user.DateOfBirth.Should().Be(new DateOnly(2014, 9, 10));
        user.HideUnratedTitles.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldStoreHideUnratedTitles()
    {
        await _handler.Handle(new UpdateUserAgeRestrictionCommand
        {
            UserId = _userId,
            Enabled = true,
            DateOfBirth = new DateOnly(2014, 9, 10),
            HideUnratedTitles = false
        }, CancellationToken.None);

        var user = await _context.Users.SingleAsync(u => u.Id == _userId);
        user.HideUnratedTitles.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenUserMissing()
    {
        var act = () => _handler.Handle(new UpdateUserAgeRestrictionCommand
        {
            UserId = Guid.NewGuid(),
            Enabled = false
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
