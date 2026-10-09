using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
using K7.Server.Domain.Entities.Medias;
using Microsoft.Extensions.Logging;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Libraries.Commands.RefreshLibraryMetadata;

public record RefreshLibraryMetadataCommand(Guid LibraryId) : IRequest<int>;

public class RefreshLibraryMetadataCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    ILogger<RefreshLibraryMetadataCommandHandler> logger)
    : IRequestHandler<RefreshLibraryMetadataCommand, int>
{
    public async Task<int> Handle(RefreshLibraryMetadataCommand request, CancellationToken cancellationToken)
    {
        var library = await context.Libraries
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.LibraryId, cancellationToken);

        Guard.Against.NotFound(request.LibraryId, library);

        if (library.PeerServerId is not null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(request.LibraryId),
                    "Federated libraries cannot refresh metadata locally.")
            ]);
        }

        var mediaIds = await context.Medias
            .AsNoTracking()
            .Where(m => m is Movie || m is MusicAlbum || m is Serie || m is MusicArtist)
            .Where(m => context.MediaLibraryAvailabilities.Any(a => a.MediaId == m.Id && a.LibraryId == library.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var queued = 0;
        foreach (var mediaId in mediaIds)
        {
            try
            {
                await sender.Send(new QueueRefreshMediaMetadataCommand { MediaId = mediaId }, cancellationToken);
                queued++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to queue metadata refresh for media {MediaId} in library {LibraryId}", mediaId, library.Id);
            }
        }

        logger.LogInformation(
            "Queued metadata refresh for {Queued} of {Total} media in library {LibraryId}",
            queued,
            mediaIds.Count,
            library.Id);

        return queued;
    }
}
