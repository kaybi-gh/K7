using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Events;

namespace MediaServer.Application.Features.Libraries.Commands.DeleteIndexedFile;

public record DeleteIndexedFileCommand(Guid Id) : IRequest;

public class DeleteIndexedFileCommandHandler : IRequestHandler<DeleteIndexedFileCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteIndexedFileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteIndexedFileCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.IndexedFiles.Remove(entity);
        entity.AddDomainEvent(new IndexedFileDeletedEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);

        // TODO - Clean metadas, pictures, stats if asked?
    }
}
