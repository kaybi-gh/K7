using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;

public class ReidentifyIndexedFileCommand : IRequest
{
    public required Guid IndexedFileId { get; init; }
    public required string SelectedProvider { get; init; }
    public required string SelectedExternalId { get; init; }
}

public class ReidentifyIndexedFileCommandHandler(IApplicationDbContext context, ISender sender)
    : IRequestHandler<ReidentifyIndexedFileCommand>
{
    public async Task Handle(ReidentifyIndexedFileCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await context.IndexedFiles
            .FirstOrDefaultAsync(f => f.Id == request.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);

        if (indexedFile.MediaId.HasValue)
        {
            var oldMedia = await context.Medias
                .Include(m => m.IndexedFiles)
                .FirstOrDefaultAsync(m => m.Id == indexedFile.MediaId.Value, cancellationToken);

            if (oldMedia != null)
            {
                oldMedia.IndexedFiles?.Remove(indexedFile);
                // We keep the old media as is. It might have playback progress or other files attached to it.
            }
            indexedFile.MediaId = null;
        }

        // Try to fetch existing Media by the new external id
        var existingExternalId = await context.ExternalIds
            .Include(x => x!.Media)
                .ThenInclude(x => x!.IndexedFiles)
            .FirstOrDefaultAsync(x => x.Value == request.SelectedExternalId && x.ProviderName == request.SelectedProvider, cancellationToken);

        if (existingExternalId != null && existingExternalId.Media != null)
        {
            existingExternalId.Media.IndexedFiles ??= [];
            existingExternalId.Media.IndexedFiles.Add(indexedFile);
            await context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (context.Entry(indexedFile).State == EntityState.Detached)
        {
            context.IndexedFiles.Attach(indexedFile);
        }

        // TODO - TV/Music based on Library type
        BaseMedia newMedia = new Movie() { IndexedFiles = [indexedFile] };

        context.Medias.Add(newMedia);
        newMedia.AddDomainEvent(new MediaCreatedEvent(newMedia));
        await context.SaveChangesAsync(cancellationToken);

        await sender.Send(new CreateBackgroundTaskCommand()
        {
            Request = new RefreshMediaMetadatasCommand()
            {
                MediaId = newMedia.Id,
                MetadataProviderExternalId = request.SelectedExternalId,
                Language = "fr", // TODO - Take langage from config
                FallbackLanguage = "en"
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = newMedia.Id,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxRetryCount = 1
        }, cancellationToken);
    }
}
