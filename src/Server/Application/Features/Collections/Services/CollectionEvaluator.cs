using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Features.DynamicPlaylists.Services;
using K7.Server.Application.Features.Medias.Queries.Common;
using K7.Server.Domain.Entities.Collections;

namespace K7.Server.Application.Features.Collections.Services;

internal static class CollectionEvaluator
{
    public static async Task RebuildItemsAsync(
        IApplicationDbContext context,
        Collection collection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (collection.RuleFilter is null || collection.MediaType is null)
            throw new ValidationException("Collection is not dynamic.");

        var query = context.Medias.AsNoTracking().AsQueryable();

        if (collection.LibraryGroupId is { } groupId)
        {
            var libraryIds = await LibraryGroupFilterHelper.ResolveLibraryIdsAsync(
                context, null, [groupId], cancellationToken);
            if (libraryIds is not { Length: > 0 })
            {
                await ReplaceItemsAsync(context, collection, [], cancellationToken);
                return;
            }

            query = query.WhereAvailableInLibraries(context, libraryIds);
        }

        var mediaIds = await DynamicPlaylistEvaluator.ApplyRules(
                query,
                collection.MediaType.Value,
                collection.RuleFilter,
                collection.OrderBy,
                collection.OrderDescending,
                collection.Limit,
                userId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        await ReplaceItemsAsync(context, collection, mediaIds, cancellationToken);
    }

    private static async Task ReplaceItemsAsync(
        IApplicationDbContext context,
        Collection collection,
        IReadOnlyList<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var existing = await context.CollectionItems
            .Where(i => i.CollectionId == collection.Id)
            .ToListAsync(cancellationToken);
        context.CollectionItems.RemoveRange(existing);
        collection.Items.Clear();

        for (var i = 0; i < mediaIds.Count; i++)
        {
            collection.Items.Add(new CollectionItem
            {
                CollectionId = collection.Id,
                MediaId = mediaIds[i],
                Order = i
            });
        }

        collection.LastEvaluatedAt = DateTimeOffset.UtcNow;
    }
}
