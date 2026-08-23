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

        var refreshMedia = await MediaMetadataRefreshTargetHelper.ResolveRefreshMediaAsync(context, media, cancellationToken);
        if (refreshMedia.Id != media.Id)
        {
            await context.Entry(refreshMedia).Collection(m => m.ExternalIds).LoadAsync(cancellationToken);
            media = refreshMedia;
        }

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

        // Library mode "auto" is not a keyed ISerieMetadataProvider. Use the concrete provider
        // that owns the resolved external id (tmdb/tvdb/...), not the library setting.
        var refreshProviderName = media is Serie serie
            ? SerieMetadataProviderCascade.ResolveKeyedProviderName(
                externalId.ProviderName,
                serie.NumberingProviderName,
                media.ExternalIds,
                externalId.Value)
            : MetadataProviderHostMapper.NormalizeProviderName(externalId.ProviderName);

        await sender.Send(new CreateBackgroundTaskCommand
        {
            Request = new RefreshMediaMetadatasCommand
            {
                MediaId = media.Id,
                MetadataProviderExternalId = externalId.Value,
                MetadataProviderName = refreshProviderName,
                Language = library.MetadataLanguage,
                FallbackLanguage = library.MetadataFallbackLanguage
            },
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            Lane = BackgroundTaskLane.Metadata,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(refreshProviderName),
            WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
            TriggeredBy = BackgroundTaskTriggeredBy.User,
            MaxAttempts = 3
        }, cancellationToken);
    }
}
