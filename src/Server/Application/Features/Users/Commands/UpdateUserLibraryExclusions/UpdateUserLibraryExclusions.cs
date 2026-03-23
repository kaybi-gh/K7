using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserLibraryExclusions;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserLibraryExclusionsCommand : IRequest
{
    public required Guid Id { get; init; }
    public required List<Guid> ExcludedLibraryIds { get; init; }
}

public class UpdateUserLibraryExclusionsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateUserLibraryExclusionsCommand>
{
    public async Task Handle(UpdateUserLibraryExclusionsCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.UserLibraryExclusions
            .Where(e => e.UserId == request.Id)
            .ToListAsync(cancellationToken);

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        context.UserLibraryExclusions.RemoveRange(existing);

        context.UserLibraryExclusions.AddRange(request.ExcludedLibraryIds.Select(libraryId => new UserLibraryExclusion
        {
            Id = Guid.NewGuid(),
            UserId = request.Id,
            LibraryId = libraryId
        }));

        await context.SaveChangesAsync(cancellationToken);
    }
}
