using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

        // Update or add external Id
        var existingExternalId = media.ExternalIds?.FirstOrDefault(x => x.ProviderName == request.SelectedProvider);
        if (existingExternalId != null)
        {
            existingExternalId.Value = request.SelectedExternalId;
        }
        else
        {
            media.ExternalIds ??= new List<ExternalId>();
            media.ExternalIds.Add(new ExternalId { ProviderName = request.SelectedProvider, Value = request.SelectedExternalId });
        }

        await context.SaveChangesAsync(cancellationToken);

        // Queue background task to fetch metadata
        await sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new RefreshMediaMetadatasCommand()
            {
                MediaId = media.Id,
                MetadataProviderExternalId = request.SelectedExternalId,
                Language = "fr", // TODO - Take langage from config
                FallbackLanguage = "en"
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = media.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 1,
            ConcurrencyGroup = ConcurrencyGroups.Tmdb
        }, cancellationToken);
    }
}