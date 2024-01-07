using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Libraries.Commands.DeleteLibrary;

public record DeleteLibraryCommand(int Id) : IRequest;

public class DeleteLibraryCommandHandler : IRequestHandler<DeleteLibraryCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteLibraryCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteLibraryCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .FindAsync(new object[] { request.Id }, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.Libraries.Remove(entity);
        entity.AddDomainEvent(new LibraryDeletedEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);
    }

}
