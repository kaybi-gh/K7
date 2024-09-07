using MediaServer.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using MediaServer.Application.Features.MetadataPictures.Commands.DownloadMetadataPictureFromProvider;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Features.MetadataPictures.EventHandlers;

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
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);

        var priority = notification.MetadataPicture.Type switch
        {
            MetadataPictureType.Backdrop => BackgroundTaskPriority.VeryLow,
            MetadataPictureType.Logo => BackgroundTaskPriority.VeryLow,
            MetadataPictureType.Poster => BackgroundTaskPriority.Low,
            _ => BackgroundTaskPriority.Lowest
        };

        await _sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new DownloadMetadataPictureFromProviderCommand() { MetadataPicture = notification.MetadataPicture },
            Priority = priority,
            TargetEntityTypeName = nameof(MetadataPicture),
            MaxRetryCount = 5
        }, cancellationToken);
    }
}
