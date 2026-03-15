using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Libraries.Commands.DeleteLibrary;

[Authorize(Roles = Roles.Administrator)]
public record DeleteLibraryCommand(Guid Id) : IRequest;

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
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.Libraries.Remove(entity);
        entity.AddDomainEvent(new LibraryDeletedEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
