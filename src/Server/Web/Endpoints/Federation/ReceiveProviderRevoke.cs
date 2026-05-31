using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class ReceiveProviderRevoke : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/provider-revoke-notify", async (
            [FromBody] ProviderRevokeRequest body,
            [FromServices] IApplicationDbContext context,
            [FromServices] IFederationNotifier federationNotifier,
            CancellationToken cancellationToken) =>
        {
            var normalizedUrl = body.ProviderUrl.TrimEnd('/');

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.BaseUrl == normalizedUrl && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.NotFound();

            peer.Status = PeerStatus.Revoked;
            await context.SaveChangesAsync(cancellationToken);

            await federationNotifier.NotifyPeerStateChangedAsync(peer.Id, PeerStatus.Revoked, cancellationToken);

            return Results.Ok();
        })
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record ProviderRevokeRequest(string ProviderUrl);
