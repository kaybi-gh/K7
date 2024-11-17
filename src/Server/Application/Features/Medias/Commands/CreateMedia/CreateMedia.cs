using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Features.Medias.Commands.CreateMedia;

public record CreateMediaCommand : IRequest<Guid>
{
    public required MediaType MediaType { get; init; }
    public required Guid IndexedFileId { get; init; }
}

public class CreateMediaCommandHandler : IRequestHandler<CreateMediaCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IMovieMetadataProvider _metadataProvider;

    public CreateMediaCommandHandler(IApplicationDbContext context, ISender sender, IMovieMetadataProvider metadataProvider)
    {
        _context = context;
        _sender = sender;
        _metadataProvider = metadataProvider;
    }

    public async Task<Guid> Handle(CreateMediaCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .FindAsync([request.IndexedFileId], cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var metadataProviderExternalId = await _metadataProvider.SearchMetadataProviderExternalIdAsync(indexedFile.Identification!, cancellationToken);

        // Try to fetch existing Media
        if (!string.IsNullOrEmpty(metadataProviderExternalId))
        {
            var existingExternalId = await _context.ExternalIds
                .Include(x => x.Metadata)
                    .ThenInclude(x => x!.Media)
                        .ThenInclude(x => x!.IndexedFiles)
                .FirstOrDefaultAsync(x => x.Value == metadataProviderExternalId, cancellationToken);

            if (existingExternalId != null
                && existingExternalId.Metadata != null
                && existingExternalId.Metadata.Media != null
                && existingExternalId.Metadata.Media.IndexedFiles != null)
            {
                existingExternalId.Metadata.Media.IndexedFiles.Add(indexedFile);
                await _context.SaveChangesAsync(cancellationToken);
                return existingExternalId.Metadata.Media.Id;
            }
        }

        if (_context.Entry(indexedFile).State == EntityState.Detached)
        {
            _context.IndexedFiles.Attach(indexedFile);
        }

        // Create new media
        BaseMedia media = request.MediaType switch
        {
            MediaType.Movie => new Movie() { IndexedFiles = [indexedFile] },
            _ => throw new NotImplementedException()
        };

        try
        {
            _context.Medias.Add(media);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch(Exception ex)
        {
            var test = ex.ToString();
        }

        if (metadataProviderExternalId != null)
        {
            await _sender.Send(new CreateBackgroundTaskCommand()
            {
                Request = new RefreshMediaMetadatasCommand()
                {
                    MediaId = media.Id,
                    MetadataProviderExternalId = metadataProviderExternalId,
                    Language = "fr",
                    FallbackLanguage = "en"
                },
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = media.Id,
                TargetEntityTypeName = nameof(BaseMedia),
                MaxRetryCount = 1
            }, cancellationToken);
        }

        media.AddDomainEvent(new MediaCreatedEvent(media));
        await _context.SaveChangesAsync(cancellationToken);
        return media.Id;
    }
}
