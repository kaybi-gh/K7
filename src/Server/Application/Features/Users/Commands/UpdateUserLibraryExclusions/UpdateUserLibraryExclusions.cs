using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserLibraryExclusions;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserLibraryExclusionsCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<Guid> ExcludedLibraryIds { get; init; }
}

public class UpdateUserLibraryExclusionsCommandHandler(
    IApplicationDbContext context,
    IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<UpdateUserLibraryExclusionsCommand>
{
    public async Task Handle(UpdateUserLibraryExclusionsCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        var existing = await context.UserLibraryExclusions
            .Where(e => e.UserId == request.Id)
            .ToListAsync(cancellationToken);

        var existingDict = existing.ToDictionary(e => e.LibraryId);
        var requestedSet = request.ExcludedLibraryIds.ToHashSet();

        foreach (var libraryId in requestedSet)
        {
            if (existingDict.TryGetValue(libraryId, out var row))
            {
                row.IsAdminExcluded = true;
            }
            else
            {
                context.UserLibraryExclusions.Add(new UserLibraryExclusion
                {
                    Id = Guid.NewGuid(),
                    UserId = request.Id,
                    LibraryId = libraryId,
                    IsAdminExcluded = true
                });
            }
        }

        foreach (var row in existing.Where(e => !requestedSet.Contains(e.LibraryId)))
        {
            row.IsAdminExcluded = false;
            if (!row.IsSelfExcluded)
                context.UserLibraryExclusions.Remove(row);
        }

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();
    }
}
