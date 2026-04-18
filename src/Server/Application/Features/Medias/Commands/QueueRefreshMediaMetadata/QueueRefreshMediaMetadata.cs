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
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var externalId = media.ExternalIds?.FirstOrDefault();
        Guard.Against.NotFound(request.MediaId, externalId, $"Media {request.MediaId} has no external ID.");

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = media.Id,
                MetadataProviderExternalId = externalId.Value,
                MetadataProviderName = externalId.ProviderName,
                Language = "fr",
                FallbackLanguage = "en"
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 1,
            ConcurrencyGroup = externalId.ProviderName
        }, cancellationToken);
    }
}
