using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationSocialCollections;

public record GetFederationSocialCollectionsQuery(string? ClientId, string? ViewerAssertion, Guid OriginUserId)
    : IRequest<IReadOnlyList<FederatedCollectionDto>>;

public class GetFederationSocialCollectionsQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    IUserFederationPrivacyService privacyService,
    IContentVisibilityEvaluator visibilityEvaluator)
    : IRequestHandler<GetFederationSocialCollectionsQuery, IReadOnlyList<FederatedCollectionDto>>
{
    public async Task<IReadOnlyList<FederatedCollectionDto>> Handle(
        GetFederationSocialCollectionsQuery request,
        CancellationToken cancellationToken)
    {
        var resolved = await peerAuthorization.ResolvePeerWithViewerAsync(
            request.ClientId, request.ViewerAssertion, cancellationToken);
        var (peer, viewer) = resolved!.Value;

        if (!await visibilityEvaluator.IsFederationSocialEnabledAsync(
                FederationContentType.Collections, outbound: false, peer.Id, cancellationToken))
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
                FederationContentType.Collections,
                privacy.Share.Collections,
                cancellationToken: cancellationToken))
            return [];

        var collections = await context.Collections
            .AsNoTracking()
            .Include(c => c.Items)
                .ThenInclude(i => i.Media)
                    .ThenInclude(m => m!.ExternalIds)
            .Where(c => c.UserId == owner.Id && c.VisibilityScope != VisibilityScope.Nobody)
            .ToListAsync(cancellationToken);

        var dtos = new List<FederatedCollectionDto>();
        foreach (var collection in collections)
        {
            if (!await visibilityEvaluator.CanViewFederatedAsync(
                    viewer.OriginUserId,
                    peer.Id,
                    owner.Id,
                    FederationContentType.Collections,
                    collection.VisibilityScope,
                    collectionId: collection.Id,
                    cancellationToken: cancellationToken))
                continue;

            dtos.Add(new FederatedCollectionDto
            {
                Id = collection.Id,
                Title = collection.Title,
                Description = collection.Description,
                MediaType = collection.MediaType,
                Items = collection.Items
                    .OrderBy(i => i.Order)
                    .Select(i => new FederatedCollectionItemDto
                    {
                        Media = i.Media!.ToFederatedMediaRef(),
                        Order = i.Order
                    })
                    .ToList()
            });
        }

        return dtos;
    }
}
