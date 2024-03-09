using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;
using MediaServer.Application.Services;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Features.Medias.Commands.RefreshMediaMetadatas;

public record RefreshMediaMetadatasCommand : IRequest
{
    public required int Id { get; init; }
}

public class RefreshMediaMetadatasCommandHandler : IRequestHandler<RefreshMediaMetadatasCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly TMDbMetadataProvider _metadataProvider;

    public RefreshMediaMetadatasCommandHandler(IApplicationDbContext context, ISender sender, TMDbMetadataProvider metadataProvider)
    {
        _context = context;
        _sender = sender;
        _metadataProvider = metadataProvider;
    }

    public async Task Handle(RefreshMediaMetadatasCommand request, CancellationToken cancellationToken)
    {
        var media = await _context.Medias
            .FindAsync([request.Id], cancellationToken);
        Guard.Against.NotFound(request.Id, media);

        var metadata = media.Type switch
        {
            MediaType.Movie => await _metadataProvider.FetchMovieMetadata((Movie)media, cancellationToken),
            _ => throw new NotImplementedException()
        };

        media.Metadata = metadata;
        //entity.AddDomainEvent(new LibraryCreatedEvent(entity));
        //await _context.Libraries.AddAsync(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
