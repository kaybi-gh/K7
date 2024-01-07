using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Libraries.Commands.CreateLibrary;

public record CreateLibraryCommand : IRequest<int>
{
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
}

public class CreateLibraryCommandHandler : IRequestHandler<CreateLibraryCommand, int>
{
    private readonly IApplicationDbContext _context;

    public CreateLibraryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> Handle(CreateLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = new Library
        {
            Title = request.Title,
            MediaType = request.MediaType,
            RootPath = request.RootPath
        };

        _context.Libraries.Add(entity);
        entity.AddDomainEvent(new LibraryCreatedEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
