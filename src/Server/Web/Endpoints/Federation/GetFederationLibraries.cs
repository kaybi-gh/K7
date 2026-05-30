using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationLibraries : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/libraries", async (
            [FromServices] IApplicationDbContext context,
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

            var sharedLibraryIds = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound && a.IsEnabled)
                .Select(a => a.LibraryId)
                .ToListAsync(cancellationToken);

            var libraries = await context.Libraries
                .Where(l => sharedLibraryIds.Contains(l.Id) && l.PeerServerId == null)
                .Select(l => new PeerLibraryDto
                {
                    Id = l.Id,
                    Title = l.Title,
                    MediaType = l.MediaType,
                    MediaCount = l.IndexedFiles.Select(f => f.MediaId).Distinct().Count()
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(libraries);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
