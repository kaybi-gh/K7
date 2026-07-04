using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;
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

        var priority = notification.MetadataPicture.Type switch
        {
            MetadataPictureType.Backdrop => BackgroundTaskPriority.VeryLow,
            MetadataPictureType.Logo => BackgroundTaskPriority.VeryLow,
            MetadataPictureType.Poster or MetadataPictureType.Cover => BackgroundTaskPriority.Low,
            _ => BackgroundTaskPriority.Lowest
        };

        var concurrencyGroup = await GetProviderGroupAsync(notification.MetadataPicture.OriginalRemoteUri, cancellationToken);

        await _sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new DownloadMetadataPictureFromProviderCommand()
            {
                Id = notification.MetadataPicture.Id
            },
            Priority = priority,
            TargetEntityId = notification.MetadataPicture.Id,
            TargetEntityTypeName = nameof(MetadataPicture),
            MaxAttempts = 5,
            ConcurrencyGroup = concurrencyGroup
        }, cancellationToken);
    }

    private async Task<string> GetProviderGroupAsync(Uri? uri, CancellationToken cancellationToken)
    {
        if (uri is null)
            return "image-download";

        var group = uri.Host switch
        {
            "image.tmdb.org" => "tmdb",
            "coverartarchive.org" or "archive.org" => "musicbrainz",
            "upload.wikimedia.org" or "commons.wikimedia.org" => "wikimedia",
            _ => (string?)null
        };

        if (group is not null)
            return group;

        if (uri.AbsolutePath.Contains("/api/metadata-pictures/"))
        {
            var peer = await _context.PeerServers
                .FirstOrDefaultAsync(p => p.BaseUrl.Contains(uri.Host), cancellationToken);
            return peer is not null ? $"federation:{peer.Id}" : $"federation:{uri.Host}";
        }

        return "image-download";
    }
}
