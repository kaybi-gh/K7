using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Constants;
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
            .Include(m => m.IndexedFiles)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var libraryId = media.IndexedFiles?.FirstOrDefault()?.LibraryId;
        Guard.Against.NotFound(request.MediaId, libraryId, $"Media {request.MediaId} has no associated library.");

        var library = await context.Libraries.FindAsync([libraryId.Value], cancellationToken);
        Guard.Against.NotFound(libraryId.Value, library);

        var providerName = library.MetadataProviderName;
        Guard.Against.NullOrEmpty(providerName, nameof(providerName), $"Library '{library.Title}' has no metadata provider configured.");

        var externalId = media.ExternalIds?.FirstOrDefault(e => e.ProviderName == providerName);
        Guard.Against.NotFound(request.MediaId, externalId, $"Media has no external ID for provider '{providerName}'.");

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = media.Id,
                MetadataProviderExternalId = externalId.Value,
                MetadataProviderName = providerName,
                Language = "fr",
                FallbackLanguage = "en"
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 1,
            ConcurrencyGroup = providerName
        }, cancellationToken);
    }
}
