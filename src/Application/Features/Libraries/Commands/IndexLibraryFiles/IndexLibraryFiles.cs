using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;

public record IndexLibraryFilesCommand(int Id) : IRequest;

public class IndexLibraryFilesCommandHandler : IRequestHandler<IndexLibraryFilesCommand>
{
    private readonly IApplicationDbContext _context;

    public IndexLibraryFilesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(IndexLibraryFilesCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .FindAsync(new object[] { request.Id }, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.AddDomainEvent(new LibraryFilesIndexTriggeredEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);
    }

}
