using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Reviews.Commands.UpsertMediaReview;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Reviews;
using K7.Server.Domain.Entities.Users;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Reviews.Commands;

[TestFixture]
public class UpsertMediaReviewCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IMediaAccessGuard _accessGuard = null!;
    private UpsertMediaReviewCommandHandler _handler = null!;

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
        _context.Users.Add(new User { Id = _userId, DisplayName = "reviewer" });
        _context.Medias.Add(new Movie { Id = _mediaId, Title = "Test Movie" });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _accessGuard = Substitute.For<IMediaAccessGuard>();

        _handler = new UpsertMediaReviewCommandHandler(_context, _currentUser, _accessGuard);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateRatingAndReview_WhenNoneExist()
    {
        var command = new UpsertMediaReviewCommand(_mediaId, new UpsertMediaReviewRequest
        {
            Text = "Great movie",
            Emoji = K7EmojiPalette.All[0],
            Rating = 8
        });

        var reviewId = await _handler.Handle(command, CancellationToken.None);

        var review = await _context.MediaReviews.SingleAsync(r => r.Id == reviewId);
        review.Text.Should().Be("Great movie");
        review.Emoji.Should().Be(K7EmojiPalette.All[0]);
        review.UserId.Should().Be(_userId);

        var rating = await _context.Ratings.OfType<UserRating>().SingleAsync(r => r.MediaId == _mediaId);
        rating.Value.Should().Be(8);
        rating.UserId.Should().Be(_userId);
        review.UserRatingId.Should().Be(rating.Id);

        await _accessGuard.Received(1).EnsureAccessAsync(_mediaId, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldUpdateExistingRatingAndReview()
    {
        var existingRating = new UserRating
        {
            UserId = _userId,
            MediaId = _mediaId,
            Value = 5,
            MinimumValue = 0,
            MaximumValue = 10
        };
        _context.Ratings.Add(existingRating);
        await _context.SaveChangesAsync();

        var existingReview = new MediaReview
        {
            UserId = _userId,
            MediaId = _mediaId,
            UserRatingId = existingRating.Id,
            Text = "Old text",
            Emoji = null
        };
        _context.MediaReviews.Add(existingReview);
        await _context.SaveChangesAsync();

        var command = new UpsertMediaReviewCommand(_mediaId, new UpsertMediaReviewRequest
        {
            Text = "Updated text",
            Emoji = K7EmojiPalette.All[1],
            Rating = 9
        });

        var reviewId = await _handler.Handle(command, CancellationToken.None);

        reviewId.Should().Be(existingReview.Id);

        var review = await _context.MediaReviews.SingleAsync(r => r.Id == reviewId);
        review.Text.Should().Be("Updated text");
        review.Emoji.Should().Be(K7EmojiPalette.All[1]);

        var rating = await _context.Ratings.OfType<UserRating>().SingleAsync(r => r.MediaId == _mediaId);
        rating.Value.Should().Be(9);
    }

    [Test]
    public void Handle_ShouldThrowValidationException_WhenEmojiIsNotAllowed()
    {
        var command = new UpsertMediaReviewCommand(_mediaId, new UpsertMediaReviewRequest
        {
            Text = "Nice",
            Emoji = "not-an-emoji",
            Rating = 7
        });

        var act = () => _handler.Handle(command, CancellationToken.None);

        act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public void Handle_ShouldThrowForbiddenAccessException_WhenUserIsNotAuthenticated()
    {
        _currentUser.Id.Returns((Guid?)null);

        var command = new UpsertMediaReviewCommand(_mediaId, new UpsertMediaReviewRequest
        {
            Text = "Nice",
            Rating = 7
        });

        var act = () => _handler.Handle(command, CancellationToken.None);

        act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task Handle_ShouldPersistRatingOnly_WhenReviewTextIsEmpty()
    {
        var command = new UpsertMediaReviewCommand(_mediaId, new UpsertMediaReviewRequest
        {
            Text = "",
            Rating = 6
        });

        var ratingId = await _handler.Handle(command, CancellationToken.None);

        var rating = await _context.Ratings.OfType<UserRating>().SingleAsync(r => r.MediaId == _mediaId);
        rating.Value.Should().Be(6);
        rating.Id.Should().Be(ratingId);

        (await _context.MediaReviews.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldRemoveReview_WhenUpdatedWithoutTextOrEmoji()
    {
        var existingRating = new UserRating
        {
            UserId = _userId,
            MediaId = _mediaId,
            Value = 5,
            MinimumValue = 0,
            MaximumValue = 10
        };
        _context.Ratings.Add(existingRating);
        await _context.SaveChangesAsync();

        _context.MediaReviews.Add(new MediaReview
        {
            UserId = _userId,
            MediaId = _mediaId,
            UserRatingId = existingRating.Id,
            Text = "Old text"
        });
        await _context.SaveChangesAsync();

        var command = new UpsertMediaReviewCommand(_mediaId, new UpsertMediaReviewRequest
        {
            Text = "",
            Rating = 7
        });

        await _handler.Handle(command, CancellationToken.None);

        (await _context.MediaReviews.CountAsync()).Should().Be(0);
        var rating = await _context.Ratings.OfType<UserRating>().SingleAsync(r => r.MediaId == _mediaId);
        rating.Value.Should().Be(7);
    }
}
