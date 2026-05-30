using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.LibraryGroups.Commands.DeleteLibraryGroup;

[Authorize(Roles = Roles.Administrator)]
public record DeleteLibraryGroupCommand(Guid Id) : IRequest;

public class DeleteLibraryGroupCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteLibraryGroupCommand>
{
    public async Task Handle(DeleteLibraryGroupCommand request, CancellationToken cancellationToken)
    {
        var group = await context.LibraryGroups
            .Include(g => g.Libraries)
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, group);

        if (group.Libraries.Count > 0)
            throw new InvalidOperationException("Cannot delete a library group that still contains libraries.");

        context.LibraryGroups.Remove(group);
        await context.SaveChangesAsync(cancellationToken);
    }
}
