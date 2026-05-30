using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class GetFederationPlaybackHistory : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/federation/playback-history", async (
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

            // Only return history if sharing is enabled
            var sharingEnabled = await context.PeerShareAgreements
                .AnyAsync(a => a.PeerServerId == peer.Id
                    && a.Direction == ShareDirection.Outbound
                    && a.IsEnabled
                    && a.SharePlaybackHistory, cancellationToken);

            if (!sharingEnabled)
                return Results.Ok(Array.Empty<FederationPlaybackEntry>());

            var sessions = await context.StreamSessions
                .Where(s => s.PeerServerId == peer.Id && s.EndedAt != null)
                .OrderByDescending(s => s.EndedAt)
                .Take(100)
                .Select(s => new FederationPlaybackEntry
                {
                    FileId = s.IndexedFileId,
                    UserDisplayName = s.User != null ? s.User.DisplayName ?? "Unknown" : "Unknown",
                    Position = s.Position,
                    EndedAt = s.EndedAt!.Value
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(sessions);
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record FederationPlaybackEntry
{
    public required Guid FileId { get; init; }
    public required string UserDisplayName { get; init; }
    public required double Position { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
}
