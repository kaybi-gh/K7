using K7.Server.Application.Features.SharedProfiles.Queries.SharedProfileExists;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles.Queries;

[TestFixture]
public class SharedProfileExistsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private SharedProfileExistsQueryHandler _handler = null!;

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
        _handler = new SharedProfileExistsQueryHandler(_context);
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
        var result = await _handler.Handle(new SharedProfileExistsQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldReturnTrue_WhenSharedProfileExists()
    {
        var hostId = Guid.NewGuid();
        _context.Users.Add(new User { Id = hostId, DisplayName = "host" });
        var profileId = Guid.NewGuid();
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = profileId,
            Name = "Couple",
            HostUserId = hostId,
            CreatedByUserId = hostId
        });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new SharedProfileExistsQuery(profileId), CancellationToken.None);

        result.Should().BeTrue();
    }
}
