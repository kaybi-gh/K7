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
        if (await currentUser.GetIdAsync(cancellationToken) is not { } viewerUserId)
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

        var visibleOwnerIds = ownerPrivacyByUserId
            .Where(x => SocialViewVisibilityHelper.CanViewerSeeLocalContent(
                            viewerPrivacy,
                            FederationContentType.Reviews,
                            x.Key)
                        && x.Value.Share.Reviews != VisibilityScope.Nobody)
            .Select(x => x.Key)
            .ToList();
        var canViewEntries = await Task.WhenAll(
            visibleOwnerIds.Select(async ownerId =>
                (OwnerId: ownerId, CanView: await visibilityEvaluator.CanViewAsync(
                    viewerUserId,
                    ownerId,
                    FederationContentType.Reviews,
                    ownerPrivacyByUserId[ownerId].Share.Reviews,
                    cancellationToken: cancellationToken))));
        var canViewByOwnerId = canViewEntries.ToDictionary(x => x.OwnerId, x => x.CanView);
        var visibleReviews = new List<MediaReview>();

        foreach (var review in reviews)
        {
            if (review.UserId == viewerUserId)
            {
                visibleReviews.Add(review);
                continue;
            }

            if (canViewByOwnerId.GetValueOrDefault(review.UserId))
                visibleReviews.Add(review);
        }

        return await EnrichAsync(visibleReviews, cancellationToken);
    }

    private async Task<IReadOnlyList<MediaReviewDto>> EnrichAsync(
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

        var userIds = reviews.Select(r => r.UserId).Distinct().ToList();
        var avatarMap = userIds.Count > 0
            ? await context.MetadataPictures
                .AsNoTracking()
                .Where(p => p.UserId != null
                            && userIds.Contains(p.UserId.Value)
                            && p.Type == MetadataPictureType.UserAvatar)
                .Select(p => new { p.UserId, p.Id })
                .ToDictionaryAsync(p => p.UserId!.Value, p => p.Id, cancellationToken)
            : new Dictionary<Guid, Guid>();

        return reviews
            .Select(review =>
            {
                var dto = review.ToMediaReviewDto();
                var avatarPictureId = avatarMap.TryGetValue(review.UserId, out var pictureId)
                    ? pictureId
                    : (Guid?)null;

                if (string.IsNullOrWhiteSpace(dto.UserDisplayName) && review.User is not null)
                {
                    return dto with
                    {
                        UserDisplayName = displayNames.GetValueOrDefault(review.User.Id) ?? "?",
                        AvatarPictureId = avatarPictureId
                    };
                }

                return dto with { AvatarPictureId = avatarPictureId };
            })
            .ToList();
    }
}
