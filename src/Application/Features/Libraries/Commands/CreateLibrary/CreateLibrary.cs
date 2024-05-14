using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Features.Libraries.Commands.CreateLibrary;

public record CreateLibraryCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
    public bool TriggerFileIndexingOnCreation { get; init; } = true;
}

public class CreateLibraryCommandHandler : IRequestHandler<CreateLibraryCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public CreateLibraryCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = new Library
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            MediaType = request.MediaType,
            RootPath = request.RootPath
        };

        entity.AddDomainEvent(new LibraryCreatedEvent(entity));
        _context.Libraries.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        if (request.TriggerFileIndexingOnCreation)
        {
            var command = new IndexLibraryFilesCommand(entity.Id);
            await _sender.Send(command, cancellationToken);
        }

        return entity.Id;
    }
}
