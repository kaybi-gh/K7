using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Application.Features.Medias.Commands.CreateMedia;

public record CreateMediaCommand : IRequest<int>
{
    public required MediaType MediaType { get; init; }
    public required List<int> LibraryIds { get; init; }
    public required List<int> IndexedFileIds { get; init; }
    public required MediaIdentification MediaIdentification { get; init; }
}

public class CreateMediaCommandHandler : IRequestHandler<CreateMediaCommand, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public CreateMediaCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<int> Handle(CreateMediaCommand request, CancellationToken cancellationToken)
    {
        // Chercher l'existant (librairie, indexedFile, media)
        // Rattacher si possible
        // Sinon créer nouveau
        // Déclencher évenement pour télécharger photos

        var libraries = await _context.Libraries
            .Where(x => request.LibraryIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var indexedFiles = await _context.IndexedFiles
            .Where(x => request.IndexedFileIds.Contains(x.Id))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        BaseMedia entity = request.MediaType switch
        {
            MediaType.Movie => new Movie()
            {
                Identification = request.MediaIdentification,
                LibraryId = libraries.First().Id
            },
            _ => throw new NotImplementedException()
        };
        //entity.IndexedFiles = indexedFiles;
        await _context.Medias.AddAsync(entity, cancellationToken);

        foreach (var indexedFile in indexedFiles)
        {
            indexedFile.MediaId = entity.Id;
        }
        entity.AddDomainEvent(new MediaCreatedEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);

        //await _sender.Send(new RefreshMediaMetadatasCommand()
        //{
        //    Id = entity.Id
        //}, cancellationToken);

        return entity.Id;
    }
}
