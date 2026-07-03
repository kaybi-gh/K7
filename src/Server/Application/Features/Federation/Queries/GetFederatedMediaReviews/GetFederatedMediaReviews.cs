using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Federation.Queries.GetFederatedMediaReviews;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator},{Roles.Guest}")]
public record GetFederatedMediaReviewsQuery(Guid MediaId) : IRequest<IReadOnlyList<FederatedReviewDto>>;

public class GetFederatedMediaReviewsQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IPeerClient peerClient,
    IFederationViewerAssertionService assertionService,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator,
    IFederatedMediaResolver mediaResolver)
    : IRequestHandler<GetFederatedMediaReviewsQuery, IReadOnlyList<FederatedReviewDto>>
{
    public async Task<IReadOnlyList<FederatedReviewDto>> Handle(GetFederatedMediaReviewsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } viewerUserId)
            return [];

        var viewerPrivacy = await privacyService.GetPrivacyAsync(viewerUserId, cancellationToken);
        if (viewerPrivacy.View.Reviews is VisibilityScope.Nobody or VisibilityScope.LocalServer)
            return [];

        var peers = await context.PeerServers
            .Where(p => p.Status == PeerStatus.Active && p.OutboundClientId != null && p.OutboundClientSecret != null)
            .ToListAsync(cancellationToken);

        var allReviews = new List<FederatedReviewDto>();

        var viewer = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == viewerUserId, cancellationToken);
        var viewerName = viewer?.DisplayName;

        foreach (var peer in peers)
        {
            if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.Reviews, outbound: false, peer.Id, cancellationToken))
                continue;

            var token = await peerClient.GetAccessTokenAsync(peer.BaseUrl, peer.OutboundClientId!, peer.OutboundClientSecret!, cancellationToken);
            if (token is null)
                continue;

            var assertionSecret = peer.FederationAssertionSecret ?? peer.OutboundClientSecret!;
            var assertion = assertionService.CreateAssertion(new FederatedUserRef
            {
                OriginUserId = viewerUserId,
                DisplayName = viewerName
            }, assertionSecret);

            var remoteUsers = await peerClient.GetRemoteSocialUsersAsync(peer.BaseUrl, token, assertion, cancellationToken);
            foreach (var remoteUser in remoteUsers)
            {
                if (viewerPrivacy.View.Reviews == VisibilityScope.SpecificPeople
                    && !FederationSocialConsumerHelper.MatchesViewGrants(
                        FederationContentType.Reviews,
                        viewerPrivacy.View.Reviews,
                        viewerPrivacy.View.Grants,
                        remoteUser,
                        peer.Id))
                    continue;

                var reviews = await peerClient.GetRemoteSocialReviewsAsync(
                    peer.BaseUrl, token, assertion, remoteUser.OriginUserId, cancellationToken);

                foreach (var review in reviews)
                {
                    var resolution = await mediaResolver.ResolveAsync(peer.Id, review.Media, cancellationToken);
                    if (resolution.LocalMediaId != request.MediaId)
                        continue;

                    allReviews.Add(review with
                    {
                        Author = review.Author with
                        {
                            OriginPeerServerId = peer.Id,
                            DisplayName = $"{review.Author.DisplayName} @ {peer.Name}"
                        }
                    });
                }
            }
        }

        return allReviews.OrderByDescending(r => r.Created).ToList();
    }
}
