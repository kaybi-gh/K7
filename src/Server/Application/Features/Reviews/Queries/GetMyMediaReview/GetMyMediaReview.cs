using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Ratings;
using K7.Shared.Dtos.Entities.Reviews;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Queries.GetMyMediaReview;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetMyMediaReviewQuery(Guid MediaId) : IRequest<MyMediaReviewStateDto?>;

public class GetMyMediaReviewQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMyMediaReviewQuery, MyMediaReviewStateDto?>
{
    public async Task<MyMediaReviewStateDto?> Handle(GetMyMediaReviewQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return null;

        var rating = await context.Ratings
            .OfType<UserRating>()
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.MediaId == request.MediaId)
            .Select(r => (int?)r.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var review = await context.MediaReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.MediaId == request.MediaId, cancellationToken);

        if (rating is null && review is null)
            return null;

        return new MyMediaReviewStateDto
        {
            Rating = rating,
            HasReview = review is not null,
            Text = review?.Text,
            Emoji = review?.Emoji
        };
    }
}
