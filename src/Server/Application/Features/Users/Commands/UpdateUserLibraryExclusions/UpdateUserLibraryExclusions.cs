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
        var domainUser = await context.Users
            .Include(u => u.LibraryExclusions)
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, domainUser);

        domainUser.LibraryExclusions.Clear();

        foreach (var libraryId in request.ExcludedLibraryIds)
        {
            domainUser.LibraryExclusions.Add(new UserLibraryExclusion
            {
                Id = Guid.NewGuid(),
                UserId = domainUser.Id,
                LibraryId = libraryId
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
