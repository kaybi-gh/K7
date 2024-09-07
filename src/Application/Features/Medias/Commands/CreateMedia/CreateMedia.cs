using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using MediaServer.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;

namespace MediaServer.Application.Features.Medias.Commands.CreateMedia;

public record CreateMediaCommand : IRequest<Guid>
{
    public required MediaType MediaType { get; init; }
    public required IndexedFile IndexedFile { get; init; }
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
        var metadataProviderExternalId = await _metadataProvider.SearchMetadataProviderExternalIdAsync(request.IndexedFile.Identification!, cancellationToken);

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
                existingExternalId.Metadata.Media.IndexedFiles.Add(request.IndexedFile);
                await _context.SaveChangesAsync(cancellationToken);
                return existingExternalId.Metadata.Media.Id;
            }
        }

        if (_context.Entry(request.IndexedFile).State == EntityState.Detached)
        {
            _context.IndexedFiles.Attach(request.IndexedFile);
        }

        // Create new media
        BaseMedia media = request.MediaType switch
        {
            MediaType.Movie => new Movie() { IndexedFiles = [request.IndexedFile] },
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
