using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Reviews;
using K7.Shared.Constants;
using K7.Shared.Dtos.Requests;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Commands.UpsertMediaReview;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpsertMediaReviewCommand(Guid MediaId, UpsertMediaReviewRequest Request) : IRequest<Guid>;

public class UpsertMediaReviewCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<UpsertMediaReviewCommand, Guid>
{
    public async Task<Guid> Handle(UpsertMediaReviewCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            throw new ForbiddenAccessException();

        await accessGuard.EnsureAccessAsync(command.MediaId, cancellationToken);

        if (command.Request.Rating <= 0)
            throw new Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("Rating", "Rating is required.")]);

        if (!string.IsNullOrWhiteSpace(command.Request.Emoji) && !K7EmojiPalette.IsAllowed(command.Request.Emoji))
            throw new Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("Emoji", "Emoji is not allowed.")]);

        var rating = await context.Ratings
            .OfType<UserRating>()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == command.MediaId, cancellationToken);

        if (rating is null)
        {
            rating = new UserRating
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MediaId = command.MediaId,
                Value = command.Request.Rating,
                MinimumValue = 0,
                MaximumValue = 10
            };
            context.Ratings.Add(rating);
        }
        else
        {
            rating.Value = command.Request.Rating;
        }

        var review = await context.MediaReviews
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == command.MediaId, cancellationToken);

        var text = command.Request.Text.Trim();
        var hasReviewContent = !string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(command.Request.Emoji);

        if (!hasReviewContent)
        {
            if (review is not null)
                context.MediaReviews.Remove(review);

            await context.SaveChangesAsync(cancellationToken);
            return rating.Id;
        }

        if (review is null)
        {
            review = new MediaReview
            {
                UserId = userId,
                MediaId = command.MediaId,
                UserRatingId = rating.Id,
                Text = text,
                Emoji = command.Request.Emoji
            };
            context.MediaReviews.Add(review);
        }
        else
        {
            review.Text = text;
            review.Emoji = command.Request.Emoji;
            review.UserRatingId = rating.Id;
        }

        await context.SaveChangesAsync(cancellationToken);
        return review.Id;
    }
}
