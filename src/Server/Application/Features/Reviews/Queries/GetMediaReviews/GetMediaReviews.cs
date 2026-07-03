using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Reviews;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Reviews;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Reviews.Queries.GetMediaReviews;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator},{Roles.Guest}")]
public record GetMediaReviewsQuery(Guid MediaId) : IRequest<IReadOnlyList<MediaReviewDto>>;

public class GetMediaReviewsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator,
    IIdentityService identityService)
    : IRequestHandler<GetMediaReviewsQuery, IReadOnlyList<MediaReviewDto>>
{
    public async Task<IReadOnlyList<MediaReviewDto>> Handle(GetMediaReviewsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } viewerUserId)
            return [];

        var reviews = await context.MediaReviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.UserRating)
            .Where(r => r.MediaId == request.MediaId)
            .OrderByDescending(r => r.Created)
            .ToListAsync(cancellationToken);

        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        var ownerPrivacyByUserId = new Dictionary<Guid, FederationPrivacySettingsDto>();
        var visibleReviews = new List<MediaReview>();

        foreach (var review in reviews)
        {
            if (review.UserId == viewerUserId)
            {
                visibleReviews.Add(review);
                continue;
            }

            if (!SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                    viewerPrivacy,
                    FederationContentType.Reviews,
                    review.UserId))
                continue;

            if (!ownerPrivacyByUserId.TryGetValue(review.UserId, out var ownerPrivacy))
            {
                ownerPrivacy = await privacyService.GetPrivacyAsync(review.UserId, cancellationToken);
                ownerPrivacyByUserId[review.UserId] = ownerPrivacy;
            }

            if (ownerPrivacy.Share.Reviews == VisibilityScope.Nobody)
                continue;

            if (!await visibilityEvaluator.CanViewAsync(
                    viewerUserId,
                    review.UserId,
                    FederationContentType.Reviews,
                    ownerPrivacy.Share.Reviews,
                    cancellationToken: cancellationToken))
                continue;

            visibleReviews.Add(review);
        }

        return await EnrichDisplayNamesAsync(visibleReviews, cancellationToken);
    }

    private async Task<IReadOnlyList<MediaReviewDto>> EnrichDisplayNamesAsync(
        IReadOnlyList<MediaReview> reviews,
        CancellationToken cancellationToken)
    {
        var results = new List<MediaReviewDto>(reviews.Count);

        foreach (var review in reviews)
        {
            var dto = review.ToMediaReviewDto();
            if (!string.IsNullOrWhiteSpace(dto.UserDisplayName) || review.User is null)
            {
                results.Add(dto);
                continue;
            }

            results.Add(dto with
            {
                UserDisplayName = await LocalUserDisplayNameHelper.ResolveAsync(
                    identityService,
                    review.User,
                    cancellationToken)
            });
        }

        return results;
    }
}
