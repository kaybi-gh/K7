using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.ToggleMediaExclusion;
using K7.Server.Application.Features.Users.Queries.GetSelfMediaExclusions;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users;

[TestFixture]
public class MediaExclusionVisibilityTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IIdentityService _identity = null!;
    private Guid _userId;
    private Guid _selfHiddenId;
    private Guid _adminHiddenId;

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
        _selfHiddenId = Guid.NewGuid();
        _adminHiddenId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, IdentityUserId = "identity", DisplayName = "viewer" });
        _context.Medias.AddRange(
            new Movie { Id = _selfHiddenId, Title = "Mine" },
            new Movie { Id = _adminHiddenId, Title = "Locked" });
        _context.UserMediaExclusions.AddRange(
            new UserMediaExclusion
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _selfHiddenId,
                IsSelfExcluded = true
            },
            new UserMediaExclusion
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                MediaId = _adminHiddenId,
                IsAdminExcluded = true
            });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("identity");
        _identity = Substitute.For<IIdentityService>();
        _identity.IsInRoleAsync("identity", Roles.Administrator).Returns(false);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task GetSelfMediaExclusions_ShouldOmitAdminHidden_WhenCallerIsNotAdmin()
    {
        var handler = new GetSelfMediaExclusionsQueryHandler(_context, _currentUser, _identity);

        var result = await handler.Handle(new GetSelfMediaExclusionsQuery(), CancellationToken.None);

        result.Select(m => m.Id).Should().Equal(_selfHiddenId);
    }

    [Test]
    public async Task GetSelfMediaExclusions_ShouldIncludeAdminHidden_WhenCallerIsAdmin()
    {
        _identity.IsInRoleAsync("identity", Roles.Administrator).Returns(true);
        var handler = new GetSelfMediaExclusionsQueryHandler(_context, _currentUser, _identity);

        var result = await handler.Handle(new GetSelfMediaExclusionsQuery(), CancellationToken.None);

        result.Select(m => m.Id).Should().BeEquivalentTo([_selfHiddenId, _adminHiddenId]);
    }

    [Test]
    public async Task Toggle_ShouldForbidUnhide_WhenAdminHidMediaAndCallerIsNotAdmin()
    {
        var handler = CreateToggleHandler();

        var act = () => handler.Handle(new ToggleMediaExclusionCommand { MediaId = _adminHiddenId }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        var row = await _context.UserMediaExclusions.SingleAsync(e => e.MediaId == _adminHiddenId);
        row.IsAdminExcluded.Should().BeTrue();
    }

    [Test]
    public async Task Toggle_ShouldClearAdminHide_WhenCallerIsAdmin()
    {
        _identity.IsInRoleAsync("identity", Roles.Administrator).Returns(true);
        var handler = CreateToggleHandler();

        var excluded = await handler.Handle(new ToggleMediaExclusionCommand { MediaId = _adminHiddenId }, CancellationToken.None);

        excluded.Should().BeFalse();
        (await _context.UserMediaExclusions.AnyAsync(e => e.MediaId == _adminHiddenId)).Should().BeFalse();
    }

    private ToggleMediaExclusionCommandHandler CreateToggleHandler() =>
        new(_context, _currentUser, Substitute.For<IMediaQueryCacheInvalidator>(), _identity);
}
