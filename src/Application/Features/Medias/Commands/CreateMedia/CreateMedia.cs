using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Features.Medias.Commands.CreateMedia;

public record CreateMediaCommand : IRequest<int>
{
    public required MediaType MediaType { get; init; }
    public required List<int> LibraryIds { get; init; }
    public required List<int> IndexedFileIds { get; init; }
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
            .ToListAsync(cancellationToken);


        var entity = new Movie
        {
            Library = libraries.First(),
            LibraryId = libraries.First().Id,
            
            //Title = request.Title,
            //MediaType = request.MediaType,
            //RootPath = request.RootPath
        };

        entity.AddDomainEvent(new MediaCreatedEvent(entity));
        await _context.Medias.AddAsync(entity);
        await _context.SaveChangesAsync(cancellationToken);

        /*if (request.TriggerFileIndexingOnCreation)
        {
            var command = new IndexLibraryFilesCommand(entity.Id);
            await _sender.Send(command, cancellationToken);
        }*/

        return entity.Id;
    }
}
