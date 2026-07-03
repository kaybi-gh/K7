using K7.Server.Domain.Entities.Reviews;
using K7.Shared.Dtos.Entities.Reviews;

namespace K7.Server.Application.Common.Mappings;

public static class ReviewMappings
{
    extension(MediaReview domain)
    {
        public MediaReviewDto ToMediaReviewDto() => new()
        {
            Id = domain.Id,
            UserId = domain.UserId,
            MediaId = domain.MediaId,
            UserRatingId = domain.UserRatingId,
            Text = domain.Text,
            Emoji = domain.Emoji,
            Rating = domain.UserRating?.Value ?? 0,
            UserDisplayName = domain.User?.DisplayName,
            Created = domain.Created,
            LastModified = domain.LastModified
        };
    }
}
