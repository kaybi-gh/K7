using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Reviews;
using K7.Server.Domain.Entities.Users;
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
        var ownerIds = reviews.Select(r => r.UserId).Distinct().Where(id => id != viewerUserId).ToList();

        var ownerPrivacyEntries = await Task.WhenAll(
            ownerIds.Select(async ownerId =>
                (OwnerId: ownerId, Privacy: await privacyService.GetPrivacyAsync(ownerId, cancellationToken))));

        var ownerPrivacyByUserId = ownerPrivacyEntries.ToDictionary(x => x.OwnerId, x => x.Privacy);

        var canViewByOwnerId = new Dictionary<Guid, bool>();
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
                continue;

            if (ownerPrivacy.Share.Reviews == VisibilityScope.Nobody)
                continue;

            if (!canViewByOwnerId.TryGetValue(review.UserId, out var canView))
            {
                canView = await visibilityEvaluator.CanViewAsync(
                    viewerUserId,
                    review.UserId,
                    FederationContentType.Reviews,
                    ownerPrivacy.Share.Reviews,
                    cancellationToken: cancellationToken);
                canViewByOwnerId[review.UserId] = canView;
            }

            if (canView)
                visibleReviews.Add(review);
        }

        return await EnrichDisplayNamesAsync(visibleReviews, cancellationToken);
    }

    private async Task<IReadOnlyList<MediaReviewDto>> EnrichDisplayNamesAsync(
        IReadOnlyList<MediaReview> reviews,
        CancellationToken cancellationToken)
    {
        var usersNeedingResolution = reviews
            .Where(r => r.User is not null && string.IsNullOrWhiteSpace(r.User.DisplayName))
            .Select(r => r.User!)
            .DistinctBy(u => u.Id)
            .ToList();

        var displayNames = await LocalUserDisplayNameHelper.ResolveManyAsync(
            identityService,
            usersNeedingResolution,
            cancellationToken);

        return reviews
            .Select(review =>
            {
                var dto = review.ToMediaReviewDto();
                if (!string.IsNullOrWhiteSpace(dto.UserDisplayName) || review.User is null)
                    return dto;

                return dto with { UserDisplayName = displayNames.GetValueOrDefault(review.User.Id) ?? "?" };
            })
            .ToList();
    }
}
