using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationSocialReviews;

public record GetFederationSocialReviewsQuery(string? ClientId, string? ViewerAssertion, Guid OriginUserId)
    : IRequest<IReadOnlyList<FederatedReviewDto>>;

public class GetFederationSocialReviewsQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator)
    : IRequestHandler<GetFederationSocialReviewsQuery, IReadOnlyList<FederatedReviewDto>>
{
    public async Task<IReadOnlyList<FederatedReviewDto>> Handle(
        GetFederationSocialReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = await peerAuthorization.ResolvePeerWithViewerAsync(
            request.ClientId, request.ViewerAssertion, cancellationToken);
        var (peer, viewer) = resolved!.Value;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.Reviews, outbound: false, peer.Id, cancellationToken))
            return [];

        var owner = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.OriginUserId && u.PeerServerId == null, cancellationToken);

        if (owner is null)
            throw new NotFoundException(request.OriginUserId.ToString(), "User");

        var privacy = await privacyService.GetPrivacyAsync(owner.Id, cancellationToken);
        if (!await visibilityEvaluator.CanViewFederatedAsync(
                viewer.OriginUserId,
                peer.Id,
                owner.Id,
                FederationContentType.Reviews,
                privacy.Share.Reviews,
                cancellationToken: cancellationToken))
            return [];

        var reviews = await context.MediaReviews
            .AsNoTracking()
            .Include(r => r.UserRating)
            .Include(r => r.Media)
                .ThenInclude(m => m!.ExternalIds)
            .Where(r => r.UserId == owner.Id)
            .OrderByDescending(r => r.Created)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => new FederatedReviewDto
        {
            Id = r.Id,
            Author = new FederatedUserRef
            {
                OriginUserId = owner.Id,
                DisplayName = owner.DisplayName
            },
            Media = r.Media!.ToFederatedMediaRef(),
            Text = r.Text,
            Emoji = r.Emoji,
            Rating = r.UserRating?.Value ?? 0,
            Created = r.Created
        }).ToList();
    }
}
