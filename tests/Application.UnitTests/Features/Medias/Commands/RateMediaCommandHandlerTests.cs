using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.RateMedia;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class RateMediaCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
    private IUserRatingNotifier _notifier = null!;
    private RateMediaCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _mediaId;

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
        _mediaId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "ident", DisplayName = "rater" });
        _context.Medias.Add(new Movie { Id = _mediaId, Title = "Film" });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("ident");
        _accessGuard = Substitute.For<IMediaAccessGuard>();
        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _notifier = Substitute.For<IUserRatingNotifier>();
        _handler = new RateMediaCommandHandler(_context, _currentUser, _accessGuard, _cacheInvalidator, _notifier);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateRatingAndNotify_WhenNoneExists()
    {
        await _handler.Handle(new RateMediaCommand(_mediaId, 8), CancellationToken.None);

        var rating = await _context.Ratings.OfType<UserRating>()
            .SingleAsync(r => r.UserId == _userId && r.MediaId == _mediaId);
        rating.Value.Should().Be(8);

        await _notifier.Received(1).NotifyUserRatingUpdatedAsync(
            "ident", _mediaId, 8, Arg.Any<CancellationToken>());
        _cacheInvalidator.Received(1).InvalidateAll();
    }

    [Test]
    public async Task Handle_ShouldUpdateRatingAndNotify_WhenExists()
    {
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _mediaId,
            Value = 4,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        await _handler.Handle(new RateMediaCommand(_mediaId, 10), CancellationToken.None);

        var rating = await _context.Ratings.OfType<UserRating>()
            .SingleAsync(r => r.UserId == _userId && r.MediaId == _mediaId);
        rating.Value.Should().Be(10);

        await _notifier.Received(1).NotifyUserRatingUpdatedAsync(
            "ident", _mediaId, 10, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldSkipNotify_WhenIdentityIdIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);

        await _handler.Handle(new RateMediaCommand(_mediaId, 6), CancellationToken.None);

        (await _context.Ratings.OfType<UserRating>().CountAsync()).Should().Be(1);
        await _notifier.DidNotReceiveWithAnyArgs().NotifyUserRatingUpdatedAsync(
            default!, default, default, default);
    }

    [Test]
    public async Task Handle_ShouldDoNothing_WhenUserIdIsMissing()
    {
        _currentUser.Id.Returns((Guid?)null);

        await _handler.Handle(new RateMediaCommand(_mediaId, 6), CancellationToken.None);

        (await _context.Ratings.OfType<UserRating>().CountAsync()).Should().Be(0);
        await _notifier.DidNotReceiveWithAnyArgs().NotifyUserRatingUpdatedAsync(
            default!, default, default, default);
    }
}
