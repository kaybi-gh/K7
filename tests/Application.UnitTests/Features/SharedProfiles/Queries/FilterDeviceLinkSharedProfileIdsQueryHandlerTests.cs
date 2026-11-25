using K7.Server.Application.Features.SharedProfiles.Queries.FilterDeviceLinkSharedProfileIds;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.SharedProfiles.Queries;

[TestFixture]
public class FilterDeviceLinkSharedProfileIdsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private FilterDeviceLinkSharedProfileIdsQueryHandler _handler = null!;

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
        _handler = new FilterDeviceLinkSharedProfileIdsQueryHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldKeepMemberships_WhenTheFormPostsOtherIds()
    {
        var memberId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        _context.Users.Add(new User { Id = memberId, IdentityUserId = "member-identity", DisplayName = "member" });
        _context.Users.Add(new User { Id = strangerId, IdentityUserId = "stranger-identity", DisplayName = "stranger" });

        var hostedId = Guid.NewGuid();
        var joinedId = Guid.NewGuid();
        var foreignId = Guid.NewGuid();
        _context.SharedProfiles.AddRange(
            new SharedProfile { Id = hostedId, Name = "Hosted", HostUserId = memberId, CreatedByUserId = memberId },
            new SharedProfile { Id = joinedId, Name = "Joined", HostUserId = strangerId, CreatedByUserId = strangerId },
            new SharedProfile { Id = foreignId, Name = "Foreign", HostUserId = strangerId, CreatedByUserId = strangerId });
        _context.SharedProfileMembers.Add(new SharedProfileMember { SharedProfileId = joinedId, UserId = memberId });
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new FilterDeviceLinkSharedProfileIdsQuery(
                "member-identity",
                [foreignId, hostedId, joinedId, Guid.NewGuid()]),
            CancellationToken.None);

        result.Should().Equal(hostedId, joinedId);
    }
}
