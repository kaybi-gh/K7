using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Reviews;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Queries.GetMyMediaReviews;

[Authorize]
public record GetMyMediaReviewsQuery : IRequest<IReadOnlyList<SocialUserReviewViewDto>>;

public class GetMyMediaReviewsQueryHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<GetMyMediaReviewsQuery, IReadOnlyList<SocialUserReviewViewDto>>
{
    private const int MaxItems = 500;

    public async Task<IReadOnlyList<SocialUserReviewViewDto>> Handle(
        GetMyMediaReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return [];

        var reviews = await context.MediaReviews
            .AsNoTracking()
            .IncludeReviewMediaDetails()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.Created)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => new SocialUserReviewViewDto
        {
            Id = r.Id,
            Text = r.Text,
            Emoji = r.Emoji,
            Rating = (int)(r.UserRating?.Value ?? 0),
            Created = r.Created,
            Media = r.Media!.ToSocialUserMediaCard(FederatedSocialItemStatus.ResolvedLocal)
        }).ToList();
    }
}
