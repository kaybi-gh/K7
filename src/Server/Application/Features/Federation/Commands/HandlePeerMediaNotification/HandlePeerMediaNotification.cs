using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Federation.Commands.SyncPeerMetadata;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Federation.Commands.HandlePeerMediaNotification;

public record HandlePeerMediaNotificationCommand(string? ClientId, PeerMediaNotifyRequest Request) : IRequest;

public class HandlePeerMediaNotificationCommandHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context,
    ISender sender,
    IMediaLibraryAvailabilityService mediaLibraryAvailabilityService)
    : IRequestHandler<HandlePeerMediaNotificationCommand>
{
    public async Task Handle(HandlePeerMediaNotificationCommand command, CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(command.ClientId, cancellationToken);
        var body = command.Request;

        switch (body.Type)
        {
            case PeerMediaNotificationType.Added:
            case PeerMediaNotificationType.Modified:
                await HandleAddedOrModifiedAsync(peer, body, cancellationToken);
                break;
            case PeerMediaNotificationType.Removed:
                await HandleRemovedAsync(peer, body, cancellationToken);
                break;
        }
    }

    private async Task HandleAddedOrModifiedAsync(
        Domain.Entities.Federation.PeerServer peer,
        PeerMediaNotifyRequest body,
        CancellationToken cancellationToken)
    {
        var federationExternalIdValue = $"{peer.Id}:{body.MediaId}";

        var existingMedia = await context.ExternalIds
            .Where(e => e.ProviderName == "federation" && e.Value == federationExternalIdValue && e.MediaId != null)
            .Select(e => e.Media)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingMedia is not null)
        {
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
            await sender.Send(new SyncPeerMetadataCommand(peer.Id), cancellationToken);
        }
    }

    private async Task HandleRemovedAsync(
        Domain.Entities.Federation.PeerServer peer,
        PeerMediaNotifyRequest body,
        CancellationToken cancellationToken)
    {
        var libraryIds = await context.RemoteIndexedFiles
            .Where(r => r.PeerServerId == peer.Id && r.RemoteMediaId == body.MediaId)
            .Select(r => r.LibraryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        await context.RemoteIndexedFiles
            .Where(r => r.PeerServerId == peer.Id && r.RemoteMediaId == body.MediaId)
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var libraryId in libraryIds)
            await mediaLibraryAvailabilityService.RebuildForLibraryAsync(libraryId, cancellationToken);
    }
}
