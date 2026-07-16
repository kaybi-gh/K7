using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.MergeUsers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class MergeUsersCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IIdentityService _identityService = null!;
    private IUser _currentUser = null!;
    private MergeUsersCommandHandler _handler = null!;

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
        _currentUser = Substitute.For<IUser>();
        _handler = new MergeUsersCommandHandler(_context, _identityService, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenMergingOwnAccountAsSource()
    {
        var identityId = "me";
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _context.Users.Add(new User { Id = sourceId, IdentityUserId = identityId, DisplayName = "me" });
        _context.Users.Add(new User { Id = targetId, IdentityUserId = "other", DisplayName = "other" });
        await _context.SaveChangesAsync();

        _currentUser.IdentityId.Returns(identityId);

        var act = () => _handler.Handle(new MergeUsersCommand(sourceId, targetId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenMergingGuestAsSource()
    {
        var guestIdentity = "guest";
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _context.Users.Add(new User { Id = sourceId, IdentityUserId = guestIdentity, DisplayName = "guest" });
        _context.Users.Add(new User { Id = targetId, IdentityUserId = "other", DisplayName = "other" });
        await _context.SaveChangesAsync();

        _currentUser.IdentityId.Returns("admin");
        _identityService.GetRolesAsync(guestIdentity).Returns([Roles.Guest]);

        var act = () => _handler.Handle(new MergeUsersCommand(sourceId, targetId), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldMergePlayCountsAdditivelyAndDeleteSource()
    {
        var sourceIdentity = "source";
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();

        _context.Users.Add(new User { Id = sourceId, IdentityUserId = sourceIdentity, DisplayName = "source" });
        _context.Users.Add(new User { Id = targetId, IdentityUserId = "target", DisplayName = "target" });
        _context.Medias.Add(new Domain.Entities.Medias.Movie { Id = mediaId, Title = "Film" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = sourceId,
            MediaId = mediaId,
            PlayCount = 3
        });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = targetId,
            MediaId = mediaId,
            PlayCount = 2
        });
        await _context.SaveChangesAsync();

        _currentUser.IdentityId.Returns("admin");
        _identityService.GetRolesAsync(sourceIdentity).Returns([Roles.User]);

        await _handler.Handle(new MergeUsersCommand(
            sourceId,
            targetId,
            new MergeStrategy { PlayCount = PlayCountMergeMode.Additive }), CancellationToken.None);

        (await _context.Users.FindAsync(sourceId)).Should().BeNull();
        var targetState = await _context.UserMediaStates.SingleAsync(s => s.UserId == targetId && s.MediaId == mediaId);
        targetState.PlayCount.Should().Be(5);
        await _identityService.Received(1).DeleteUserAsync(sourceIdentity);
    }
}
