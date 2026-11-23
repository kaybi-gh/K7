using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.EventHandlers;

/// <summary>
/// Pre-extracted subtitles live under the metadata root and are not tied to a database row:
/// nothing else removes them when their file goes away.
/// </summary>
public class IndexedFileDeletedSubtitleCacheEventHandler(
    IOptions<PathsConfiguration> pathsOptions,
    ILogger<IndexedFileDeletedSubtitleCacheEventHandler> logger) : INotificationHandler<IndexedFileDeletedEvent>
{
    public Task Handle(IndexedFileDeletedEvent notification, CancellationToken cancellationToken)
    {
        HlsSubtitleVttExtractor.DeletePersistentCache(pathsOptions.Value, notification.IndexedFile.Id, logger);
        return Task.CompletedTask;
    }
}
