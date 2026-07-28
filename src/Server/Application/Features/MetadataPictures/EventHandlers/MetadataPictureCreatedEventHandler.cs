using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.MetadataPictures.EventHandlers;

public class MetadataPictureCreatedEventHandler : INotificationHandler<MetadataPictureCreatedEvent>
{
    private readonly ILogger<MetadataPictureCreatedEventHandler> _logger;
    private readonly ISender _sender;
    private readonly IApplicationDbContext _context;

    public MetadataPictureCreatedEventHandler(
        ILogger<MetadataPictureCreatedEventHandler> logger,
        ISender sender,
        IApplicationDbContext context)
    {
        _logger = logger;
        _sender = sender;
        _context = context;
    }

    public async Task Handle(MetadataPictureCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        if (notification.MetadataPicture.Type == MetadataPictureType.Thumbnail
            && notification.MetadataPicture.OriginalRemoteUri is null)
        {
            // Thumbnails are generated locally, not downloaded (unless federated)
            return;
        }

        var workClass = notification.MetadataPicture.Type switch
        {
            MetadataPictureType.Poster or MetadataPictureType.Cover => BackgroundTaskWorkClass.CriticalEnrich,
            _ => BackgroundTaskWorkClass.Polish
        };

        // Artwork belonging to a media served by a peer is fetched from that peer, so it belongs to the
        // federation lane and is isolated per peer: an unreachable peer must not occupy the Metadata lane
        // slots that provider downloads need.
        var peerServerId = await GetOwningPeerServerIdAsync(notification.MetadataPicture.MediaId, cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new DownloadMetadataPictureFromProviderCommand()
            {
                Id = notification.MetadataPicture.Id
            },
            TargetEntityId = notification.MetadataPicture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            Lane = peerServerId is null ? BackgroundTaskLane.Metadata : BackgroundTaskLane.Federation,
            WorkClass = workClass,
            TriggeredBy = peerServerId is null ? BackgroundTaskTriggeredBy.System : BackgroundTaskTriggeredBy.Federation,
            FederationPeerId = peerServerId,
            MaxAttempts = 5
        }, cancellationToken);
    }

    private async Task<Guid?> GetOwningPeerServerIdAsync(Guid? mediaId, CancellationToken cancellationToken)
    {
        if (mediaId is null)
            return null;

        return await _context.Medias
            .AsNoTracking()
            .Where(m => m.Id == mediaId.Value)
            .Select(m => m.PeerServerId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
