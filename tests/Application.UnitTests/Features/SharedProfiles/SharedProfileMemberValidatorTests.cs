using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles;

[TestFixture]
public class SharedProfileMemberValidatorTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

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
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task EnsureValidMembersAsync_ShouldRejectTooFewMembers()
    {
        var actingUser = Guid.NewGuid();
        var act = () => SharedProfileMemberValidator.EnsureValidMembersAsync(
            _context,
            [actingUser],
            actingUser,
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task EnsureValidMembersAsync_ShouldRejectInactiveOrMissingUsers()
    {
        var actingUser = Guid.NewGuid();
        var other = Guid.NewGuid();
        _context.Users.Add(new User { Id = actingUser, DisplayName = "Kay", IsActive = true });
        await _context.SaveChangesAsync();

        var act = () => SharedProfileMemberValidator.EnsureValidMembersAsync(
            _context,
            [actingUser, other],
            actingUser,
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.Values.SelectMany(v => v).Any(m => m.Contains("invalid")));
    }

    [Test]
    public async Task EnsureValidMembersAsync_ShouldPass_WhenMembersAreActiveLocalUsers()
    {
        var actingUser = Guid.NewGuid();
        var other = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = actingUser, DisplayName = "Kay", IsActive = true },
            new User { Id = other, DisplayName = "Marie", IsActive = true });
        await _context.SaveChangesAsync();

        await SharedProfileMemberValidator.EnsureValidMembersAsync(
            _context,
            [actingUser, other],
            actingUser,
            CancellationToken.None);
    }
}
