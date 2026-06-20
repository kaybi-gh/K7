using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Commands.UpdateSelfLibraryExclusions;

public record UpdateSelfLibraryExclusionsCommand : IRequest
{
    public required IReadOnlyList<Guid> ExcludedLibraryIds { get; init; }
}

public class UpdateSelfLibraryExclusionsCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<UpdateSelfLibraryExclusionsCommand>
{
    public async Task Handle(UpdateSelfLibraryExclusionsCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);

        var existing = await context.UserLibraryExclusions
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var existingDict = existing.ToDictionary(e => e.LibraryId);
        var requestedSet = request.ExcludedLibraryIds.ToHashSet();

        foreach (var libraryId in requestedSet)
        {
            if (existingDict.TryGetValue(libraryId, out var row))
            {
                row.IsSelfExcluded = true;
            }
            else
            {
                context.UserLibraryExclusions.Add(new UserLibraryExclusion
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LibraryId = libraryId,
                    IsSelfExcluded = true
                });
            }
        }

        foreach (var row in existing.Where(e => !requestedSet.Contains(e.LibraryId)))
        {
            row.IsSelfExcluded = false;
            if (!row.IsAdminExcluded)
                context.UserLibraryExclusions.Remove(row);
        }

        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();
    }
}
