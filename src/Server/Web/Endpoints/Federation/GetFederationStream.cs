using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationStream : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapMethods("/api/federation/stream/{fileId:guid}", ["GET", "HEAD"], async (
            Guid fileId,
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

            var indexedFile = await context.IndexedFiles
                .Include(f => f.FileMetadata)
                .FirstOrDefaultAsync(f => f.Id == fileId && sharedLibraryIds.Contains(f.LibraryId), cancellationToken);

            if (indexedFile is null)
                return Results.NotFound();

            var file = new FileInfo(indexedFile.Path);
            if (!file.Exists)
                return Results.NotFound();

            // Check concurrent stream quota
            var agreement = await context.PeerShareAgreements
                .FirstOrDefaultAsync(a => a.PeerServerId == peer.Id
                    && a.LibraryId == indexedFile.LibraryId
                    && a.Direction == ShareDirection.Outbound, cancellationToken);

            if (agreement?.MaxConcurrentStreams is not null)
            {
                var activeStreams = await context.StreamSessions
                    .CountAsync(s => s.PeerServerId == peer.Id && s.EndedAt == null, cancellationToken);

                if (activeStreams >= agreement.MaxConcurrentStreams)
                    return Results.StatusCode(429);
            }

            var container = indexedFile.FileMetadata?.Container;
            var mimeType = container is not null
                && K7.Server.Domain.Constants.Constants.ContainerMimeTypeMapping.TryGetValue(container, out var mime)
                    ? mime
                    : "application/octet-stream";

            return Results.File(indexedFile.Path, contentType: mimeType, enableRangeProcessing: true);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
