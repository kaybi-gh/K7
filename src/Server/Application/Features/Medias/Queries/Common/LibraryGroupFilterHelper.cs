using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Queries.Common;

public static class LibraryGroupFilterHelper
{
    public static async Task<Guid[]?> ResolveLibraryIdsAsync(
        IApplicationDbContext context,
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        CancellationToken cancellationToken)
    {
        if (libraryGroupIds is not { Length: > 0 })
            return libraryIds;

        var resolvedFromGroups = await context.Libraries
            .AsNoTracking()
            .Where(l => libraryGroupIds.Contains(l.LibraryGroupId))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        if (libraryIds is not { Length: > 0 })
            return resolvedFromGroups.Count > 0 ? [.. resolvedFromGroups] : [];

        return libraryIds.Intersect(resolvedFromGroups).ToArray();
    }
}
