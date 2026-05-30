using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class PostFederationSession : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/sessions", async (
            [FromBody] FederationSessionRequest request,
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

            // Find or create virtual user for the remote user
            var virtualUser = await context.Users
                .FirstOrDefaultAsync(u => u.PeerServerId == peer.Id && u.DisplayName == request.UserDisplayName, cancellationToken);

            if (virtualUser is null)
            {
                virtualUser = new User
                {
                    Id = Guid.NewGuid(),
                    PeerServerId = peer.Id,
                    DisplayName = request.UserDisplayName,
                    IsActive = true
                };
                context.Users.Add(virtualUser);
            }

            // Verify the file is shared
            var sharedLibraryIds = await context.PeerShareAgreements
                .Where(a => a.PeerServerId == peer.Id && a.Direction == ShareDirection.Outbound && a.IsEnabled)
                .Select(a => a.LibraryId)
                .ToListAsync(cancellationToken);

            var fileExists = await context.IndexedFiles
                .AnyAsync(f => f.Id == request.FileId && sharedLibraryIds.Contains(f.LibraryId), cancellationToken);

            if (!fileExists)
                return Results.NotFound();

            if (request.State == PlaybackState.Ended)
            {
                // End the session
                var existing = await context.StreamSessions
                    .FirstOrDefaultAsync(s => s.IndexedFileId == request.FileId
                        && s.UserId == virtualUser.Id
                        && s.PeerServerId == peer.Id
                        && s.EndedAt == null, cancellationToken);

                if (existing is not null)
                {
                    existing.State = PlaybackState.Ended;
                    existing.Position = request.Position;
                    existing.EndedAt = DateTimeOffset.UtcNow;
                }
            }
            else
            {
                // Find or create active session
                var session = await context.StreamSessions
                    .FirstOrDefaultAsync(s => s.IndexedFileId == request.FileId
                        && s.UserId == virtualUser.Id
                        && s.PeerServerId == peer.Id
                        && s.EndedAt == null, cancellationToken);

                if (session is null)
                {
                    session = new StreamSession
                    {
                        Id = Guid.NewGuid(),
                        IndexedFileId = request.FileId,
                        DeviceId = Guid.Empty,
                        UserId = virtualUser.Id,
                        PeerServerId = peer.Id,
                        State = request.State,
                        Position = request.Position
                    };
                    context.StreamSessions.Add(session);
                }
                else
                {
                    session.State = request.State;
                    session.Position = request.Position;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public record FederationSessionRequest
{
    public required Guid FileId { get; init; }
    public required string UserDisplayName { get; init; }
    public required PlaybackState State { get; init; }
    public required double Position { get; init; }
}
