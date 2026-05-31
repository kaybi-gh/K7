using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Federation;

public class NotifyMedia : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        string groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/federation/media-notify", async (
            [FromBody] PeerMediaNotifyRequest body,
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

            switch (body.Type)
            {
                case PeerMediaNotificationType.Added:
                case PeerMediaNotificationType.Modified:
                    await HandleAddedOrModifiedAsync(peer, body, context, sender, cancellationToken);
                    break;
                case PeerMediaNotificationType.Removed:
                    await HandleRemovedAsync(peer, body, context, cancellationToken);
                    break;
            }

            return Results.Ok();
        })
        .RequireAuthorization(Policies.PeerAccess)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static async Task HandleAddedOrModifiedAsync(
        PeerServer peer,
        PeerMediaNotifyRequest body,
        IApplicationDbContext context,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var federationExternalIdValue = $"{peer.Id}:{body.MediaId}";

        var existingMedia = await context.ExternalIds
            .Where(e => e.ProviderName == "federation" && e.Value == federationExternalIdValue && e.MediaId != null)
            .Select(e => e.Media)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingMedia is not null)
        {
            // Re-fetch metadata for existing media
            var library = await context.Libraries
                .FirstOrDefaultAsync(l => l.PeerServerId == peer.Id, cancellationToken);

            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new RefreshMediaMetadatasCommand
                {
                    MediaId = existingMedia.Id,
                    MetadataProviderExternalId = federationExternalIdValue,
                    MetadataProviderName = "federation",
                    Language = library?.MetadataLanguage ?? "en",
                    FallbackLanguage = library?.MetadataFallbackLanguage ?? "en"
                },
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = existingMedia.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxAttempts = 3,
                ConcurrencyGroup = $"federation:{peer.Id}"
            }, cancellationToken);
        }
        else if (body.Type == PeerMediaNotificationType.Added)
        {
            // Trigger a full sync for this peer to pick up the new media
            await sender.Send(new Application.Features.Federation.Commands.SyncPeerMetadata.SyncPeerMetadataCommand(peer.Id), cancellationToken);
        }
    }

    private static async Task HandleRemovedAsync(
        PeerServer peer,
        PeerMediaNotifyRequest body,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var remoteFiles = await context.RemoteIndexedFiles
            .Where(r => r.PeerServerId == peer.Id && r.RemoteMediaId == body.MediaId)
            .ToListAsync(cancellationToken);

        context.RemoteIndexedFiles.RemoveRange(remoteFiles);
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PeerMediaNotifyRequest
{
    public required Guid LibraryId { get; init; }
    public required Guid MediaId { get; init; }
    public required PeerMediaNotificationType Type { get; init; }
}
