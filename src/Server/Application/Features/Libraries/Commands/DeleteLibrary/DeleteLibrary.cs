using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Libraries.Commands.DeleteLibrary;

[Authorize(Roles = Roles.Administrator)]
public record DeleteLibraryCommand(Guid Id) : IRequest;

public class DeleteLibraryCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteLibraryCommand>
{
    public async Task Handle(DeleteLibraryCommand request, CancellationToken cancellationToken)
    {
        var library = await context.Libraries
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, library);

        context.Libraries.Remove(library);
        library.AddDomainEvent(new LibraryDeletedEvent(library));
        await context.SaveChangesAsync(cancellationToken);
    }
}
