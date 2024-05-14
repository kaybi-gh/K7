using System;
using System.IO;
using System.Reflection.Metadata;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Services;
using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;
using MediaServer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaServer.Application.Features.Libraries.EventHandlers;

public class MetadataPictureCreatedEventHandler : INotificationHandler<MetadataPictureCreatedEvent>
{
    private readonly ILogger<MetadataPictureCreatedEventHandler> _logger;
    private readonly PathsConfiguration _pathsConfiguration;

    public MetadataPictureCreatedEventHandler(ILogger<MetadataPictureCreatedEventHandler> logger, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _logger = logger;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task Handle(MetadataPictureCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("MediaServer Domain Event: {DomainEvent}", notification.GetType().Name);

        var destinationFilePath = _pathsConfiguration.Metadatas;
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(notification.MetadataPicture.OriginalRemoteUri.LocalPath)}";

        if (notification.MetadataPicture.PersonId != null)
        {
            destinationFilePath = Path.Combine(destinationFilePath, "persons", $"{notification.MetadataPicture.PersonId}", fileName);
        }
        else if (notification.MetadataPicture.PersonRoleId != null)
        {
            destinationFilePath = Path.Combine(destinationFilePath, "person-roles", $"{notification.MetadataPicture.PersonRoleId}", fileName);
        }
        else if (notification.MetadataPicture.MetadataId != null)
        {
            destinationFilePath = Path.Combine(destinationFilePath, "medias", $"{notification.MetadataPicture.MetadataId}", fileName);
        }

        if (await PictureDownloaderService.TryDownloadPictureAsync(notification.MetadataPicture.OriginalRemoteUri.OriginalString, destinationFilePath))
        {
            notification.MetadataPicture.Path = destinationFilePath;
        }
    }
}
