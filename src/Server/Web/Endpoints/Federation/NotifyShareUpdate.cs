using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Commands.SyncPeerMetadata;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class NotifyShareUpdate : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/share-update-notify", async (
            [FromBody] ShareUpdateNotifyRequest body,
            [FromServices] IApplicationDbContext context,
            [FromServices] ISender sender,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientId = httpContext.User.FindFirst("sub")?.Value;
            if (clientId is null)
                return Results.Forbid();

            var peer = await context.PeerServers
                .FirstOrDefaultAsync(p => p.InboundApplicationId == clientId && p.Status == PeerStatus.Active, cancellationToken);

            if (peer is null)
                return Results.Forbid();

            // Trigger a sync to refresh local libraries from the provider
            await sender.Send(new SyncPeerMetadataCommand(peer.Id), cancellationToken);

            return Results.Ok();
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record ShareUpdateNotifyRequest(IReadOnlyList<Guid> SharedLibraryIds);
