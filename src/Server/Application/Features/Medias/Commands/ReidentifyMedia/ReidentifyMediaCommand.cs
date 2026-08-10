using Ardalis.GuardClauses;
using FluentValidation.Results;
using K7.Server.Application.Common;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Medias.Commands.ReidentifyMedia;

public class ReidentifyMediaCommand : IRequest
{
    public required Guid MediaId { get; init; }
    public required string SelectedProvider { get; init; }
    public required string SelectedExternalId { get; init; }
}

public class ReidentifyMediaCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<ReidentifyMediaCommand>
{
    public async Task Handle(ReidentifyMediaCommand request, CancellationToken cancellationToken)
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
                    $"Media {request.MediaId} is not linked to any library and cannot be reidentified.")
            ]);
        }

        var providerName = MetadataProviderHostMapper.NormalizeProviderName(request.SelectedProvider);
        if (string.IsNullOrWhiteSpace(providerName) || providerName == MetadataProviderNames.Local)
            providerName = request.SelectedProvider.Trim();

        // Update or add external Id under the provider the user actually selected (may be cascade fallback).
        var existingExternalId = media.ExternalIds?.FirstOrDefault(x =>
            string.Equals(x.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
        if (existingExternalId != null)
        {
            if (string.Equals(existingExternalId.Value, request.SelectedExternalId, StringComparison.Ordinal))
            {
                // Same identity: still refresh metadata so stale provider data can catch up.
            }
            else
            {
                existingExternalId.Value = request.SelectedExternalId;
            }
        }
        else
        {
            media.ExternalIds ??= new List<ExternalId>();
            media.ExternalIds.Add(new ExternalId { ProviderName = providerName, Value = request.SelectedExternalId });
        }

        await context.SaveChangesAsync(cancellationToken);

        // Queue background task to fetch metadata - admission key must match the provider that owns the id.
        await sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new RefreshMediaMetadatasCommand()
            {
                MediaId = media.Id,
                MetadataProviderExternalId = request.SelectedExternalId,
                MetadataProviderName = providerName,
                Language = library.MetadataLanguage,
                FallbackLanguage = library.MetadataFallbackLanguage
            },
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            Lane = BackgroundTaskLane.Metadata,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeProviderName(providerName),
            WorkClass = BackgroundTaskWorkClass.CriticalEnrich,
            TriggeredBy = BackgroundTaskTriggeredBy.User,
            MaxAttempts = 1
        }, cancellationToken);
    }
}
