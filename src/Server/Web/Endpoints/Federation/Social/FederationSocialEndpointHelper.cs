using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Features.Federation.Services;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation.Social;

internal static class FederationSocialEndpointHelper
{
    public const string ViewerAssertionHeader = "X-K7-Federation-Viewer";

    public static async Task<(PeerServer? Peer, FederatedUserRef? Viewer, IResult? Error)> ResolvePeerAndViewerAsync(
        HttpContext httpContext,
        IApplicationDbContext context,
        IFederationViewerAssertionService assertionService,
        CancellationToken cancellationToken)
    {
        var clientId = httpContext.User.FindFirst("sub")?.Value;
        if (clientId is null)
            return (null, null, Results.Forbid());

        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

        if (peer is null)
            return (null, null, Results.Forbid());

        if (string.IsNullOrWhiteSpace(peer.FederationAssertionSecret))
            return (peer, null, Results.Unauthorized());

        var assertion = httpContext.Request.Headers[ViewerAssertionHeader].FirstOrDefault();
        var viewer = assertionService.ValidateAssertion(assertion, peer.FederationAssertionSecret);
        if (viewer is null)
            return (peer, null, Results.Unauthorized());

        return (peer, viewer, null);
    }

    public static FederatedMediaRef ToMediaRef(BaseMedia media) => media.ToFederatedMediaRef();
}
