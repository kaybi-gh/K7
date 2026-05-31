using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;

[Authorize(Roles = Roles.Administrator)]
public record QueueRefreshMediaMetadataCommand : IRequest
{
    public required Guid MediaId { get; init; }
}

public class QueueRefreshMediaMetadataCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<QueueRefreshMediaMetadataCommand>
{
    public async Task Handle(QueueRefreshMediaMetadataCommand request, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var library = await FindLibraryAsync(media, cancellationToken);
        var providerName = library?.MetadataProviderName;

        // Pick the external ID matching the library's provider, fallback to first available
        var externalId = providerName is not null
            ? media.ExternalIds?.FirstOrDefault(e => e.ProviderName == providerName)
              ?? media.ExternalIds?.FirstOrDefault()
            : media.ExternalIds?.FirstOrDefault();

        Guard.Against.NotFound(request.MediaId, externalId, $"Media {request.MediaId} has no external ID.");

        var concurrencyGroup = externalId.ProviderName == "federation"
            && externalId.Value.Split(':') is [var peerId, ..]
            ? $"federation:{peerId}"
            : externalId.ProviderName;

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = media.Id,
                MetadataProviderExternalId = externalId.Value,
                MetadataProviderName = externalId.ProviderName,
                Language = library?.MetadataLanguage ?? "en",
                FallbackLanguage = library?.MetadataFallbackLanguage ?? "en"
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 1,
            ConcurrencyGroup = concurrencyGroup
        }, cancellationToken);
    }

    private async Task<Library?> FindLibraryAsync(BaseMedia media, CancellationToken cancellationToken)
    {
        // For MusicArtist, IndexedFiles are on albums, not the artist itself
        if (media is MusicArtist)
        {
            var albumId = await context.Medias.OfType<MusicAlbum>()
                .Where(a => a.ArtistId == media.Id)
                .Select(a => a.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (albumId == Guid.Empty) return null;

            return await context.Libraries
                .FirstOrDefaultAsync(l => l.IndexedFiles.Any(f => f.MediaId == albumId)
                    || l.RemoteIndexedFiles.Any(r => r.MediaId == albumId), cancellationToken);
        }

        return await context.Libraries
            .FirstOrDefaultAsync(l => l.IndexedFiles.Any(f => f.MediaId == media.Id)
                || l.RemoteIndexedFiles.Any(r => r.MediaId == media.Id), cancellationToken);
    }
}
