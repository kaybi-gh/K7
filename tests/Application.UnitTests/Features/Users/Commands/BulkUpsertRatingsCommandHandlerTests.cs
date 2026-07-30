using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Users.Commands.BulkUpsertRatings;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Features.Users.Commands;

[TestFixture]
public class BulkUpsertRatingsCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private BulkUpsertRatingsCommandHandler _handler = null!;
    private IMediaQueryCacheInvalidator _cacheInvalidator = null!;
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

        _cacheInvalidator = Substitute.For<IMediaQueryCacheInvalidator>();
        _handler = new BulkUpsertRatingsCommandHandler(_context, _cacheInvalidator);

        _userId = Guid.NewGuid();
        _mediaId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, IdentityUserId = "u1", DisplayName = "user" });
        _context.Medias.Add(new Movie { Id = _mediaId, Title = "Movie" });
        _context.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateSingleRating_WhenDuplicateMediaIdsInRequest()
    {
        var count = await _handler.Handle(new BulkUpsertRatingsCommand
        {
            UserId = _userId,
            Items =
            [
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 8 },
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 9 },
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 7 }
            ]
        }, CancellationToken.None);

        count.Should().Be(1);
        var ratings = await _context.Ratings.OfType<UserRating>()
            .Where(r => r.UserId == _userId && r.MediaId == _mediaId)
            .ToListAsync();
        ratings.Should().ContainSingle();
        ratings[0].Value.Should().Be(8);
    }

    [Test]
    public async Task Handle_ShouldNotDuplicate_WhenExistingRatingAndDuplicateMediaIds()
    {
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _mediaId,
            Value = 5,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var count = await _handler.Handle(new BulkUpsertRatingsCommand
        {
            UserId = _userId,
            Strategy = new MergeStrategy { Rating = RatingConflictMode.KeepExisting },
            Items =
            [
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 10 },
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 9 }
            ]
        }, CancellationToken.None);

        count.Should().Be(0);
        var ratings = await _context.Ratings.OfType<UserRating>()
            .Where(r => r.UserId == _userId && r.MediaId == _mediaId)
            .ToListAsync();
        ratings.Should().ContainSingle();
        ratings[0].Value.Should().Be(5);
    }

    [Test]
    public async Task Handle_ShouldOverwriteOnce_WhenDuplicateMediaIdsAndOverwriteMode()
    {
        _context.Ratings.Add(new UserRating
        {
            UserId = _userId,
            MediaId = _mediaId,
            Value = 5,
            MinimumValue = 0,
            MaximumValue = 10
        });
        await _context.SaveChangesAsync();

        var count = await _handler.Handle(new BulkUpsertRatingsCommand
        {
            UserId = _userId,
            Strategy = new MergeStrategy { Rating = RatingConflictMode.Overwrite },
            Items =
            [
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 8 },
                new BulkUpsertRatingsRequest.RatingItem { MediaId = _mediaId, Value = 9 }
            ]
        }, CancellationToken.None);

        count.Should().Be(2);
        var ratings = await _context.Ratings.OfType<UserRating>()
            .Where(r => r.UserId == _userId && r.MediaId == _mediaId)
            .ToListAsync();
        ratings.Should().ContainSingle();
        ratings[0].Value.Should().Be(9);
    }
}
