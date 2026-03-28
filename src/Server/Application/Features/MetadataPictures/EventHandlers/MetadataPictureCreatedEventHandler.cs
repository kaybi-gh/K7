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

    public MetadataPictureCreatedEventHandler(ILogger<MetadataPictureCreatedEventHandler> logger, ISender sender)
    {
        _logger = logger;
        _sender = sender;
    }

    public async Task Handle(MetadataPictureCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("K7.Server Domain Event: {DomainEvent}", notification.GetType().Name);

        if (notification.MetadataPicture.Type == MetadataPictureType.Thumbnail)
        {
            // Thumbnails are generated and not downloaded
            return;
        }

        var priority = notification.MetadataPicture.Type switch
        {
            MetadataPictureType.Backdrop => BackgroundTaskPriority.VeryLow,
            MetadataPictureType.Logo => BackgroundTaskPriority.VeryLow,
            MetadataPictureType.Poster => BackgroundTaskPriority.Low,
            _ => BackgroundTaskPriority.Lowest
        };

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
            ConcurrencyGroup = "image-download"
        }, cancellationToken);
    }
}
