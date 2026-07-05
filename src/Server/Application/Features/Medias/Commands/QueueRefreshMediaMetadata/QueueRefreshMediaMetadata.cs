using Ardalis.GuardClauses;
using FluentValidation.Results;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;

[Authorize(Roles = Roles.Administrator)]
public record QueueRefreshMediaMetadataCommand : IRequest
{
    public required Guid MediaId { get; init; }
}

public class QueueRefreshMediaMetadataCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    MediaExternalIdResolver externalIdResolver)
    : IRequestHandler<QueueRefreshMediaMetadataCommand>
{
    public async Task Handle(QueueRefreshMediaMetadataCommand request, CancellationToken cancellationToken)
    {
        var media = await context.Medias
            .Include(m => m.ExternalIds)
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        var library = await MediaLibraryLinkageHelper.FindLibraryAsync(context, media, cancellationToken);
        if (library is null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(request.MediaId),
                    $"Media {request.MediaId} is not linked to any library and cannot be refreshed.")
            ]);
        }

        var externalId = media.ExternalIds.FirstOrDefault(e =>
                string.Equals(e.ProviderName, library.MetadataProviderName, StringComparison.OrdinalIgnoreCase))
            ?? await externalIdResolver.ResolveAsync(media, library, cancellationToken);

        if (externalId is null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(request.MediaId),
                    $"Media {request.MediaId} has no external ID and could not be auto-identified from file or title metadata.")
            ]);
        }

        var concurrencyGroup = library.MetadataProviderName == "federation"
            && externalId.Value.Split(':') is [var peerId, ..]
            ? $"federation:{peerId}"
            : library.MetadataProviderName;

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = media.Id,
                MetadataProviderExternalId = externalId.Value,
                MetadataProviderName = library.MetadataProviderName,
                Language = library.MetadataLanguage,
                FallbackLanguage = library.MetadataFallbackLanguage
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 3,
            ConcurrencyGroup = concurrencyGroup
        }, cancellationToken);
    }
}
