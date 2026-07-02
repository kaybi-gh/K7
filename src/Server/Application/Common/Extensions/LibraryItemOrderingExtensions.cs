using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Playlists;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Common.Extensions;

public static class LibraryItemOrderingExtensions
{
    public static IQueryable<Playlist> ApplyOrdering(this IQueryable<Playlist> query, LibraryItemOrderingOption? orderBy, Guid userId) =>
        orderBy switch
        {
            LibraryItemOrderingOption.TitleAsc => query.OrderBy(p => p.Title).ThenByDescending(p => p.LastModified),
            LibraryItemOrderingOption.TitleDesc => query.OrderByDescending(p => p.Title).ThenByDescending(p => p.LastModified),
            LibraryItemOrderingOption.CreatedAsc => query.OrderBy(p => p.Created).ThenBy(p => p.Title),
            LibraryItemOrderingOption.CreatedDesc => query.OrderByDescending(p => p.Created).ThenBy(p => p.Title),
            LibraryItemOrderingOption.LastModifiedAsc => query.OrderBy(p => p.LastModified).ThenBy(p => p.Title),
            LibraryItemOrderingOption.LastModifiedDesc => query.OrderByDescending(p => p.LastModified).ThenBy(p => p.Title),
            LibraryItemOrderingOption.LastListenedAsc => query
                .OrderBy(p => p.UserStates.Where(s => s.UserId == userId).Select(s => s.LastListenedAt).FirstOrDefault())
                .ThenBy(p => p.Title),
            null or LibraryItemOrderingOption.LastListenedDesc or _ => query
                .OrderByDescending(p => p.UserStates.Where(s => s.UserId == userId).Select(s => s.LastListenedAt).FirstOrDefault())
                .ThenBy(p => p.Title)
        };

    public static IQueryable<Collection> ApplyOrdering(this IQueryable<Collection> query, LibraryItemOrderingOption? orderBy) =>
        orderBy switch
        {
            LibraryItemOrderingOption.TitleAsc => query.OrderBy(c => c.Title).ThenByDescending(c => c.LastModified),
            LibraryItemOrderingOption.TitleDesc => query.OrderByDescending(c => c.Title).ThenByDescending(c => c.LastModified),
            LibraryItemOrderingOption.CreatedAsc => query.OrderBy(c => c.Created).ThenBy(c => c.Title),
            LibraryItemOrderingOption.CreatedDesc => query.OrderByDescending(c => c.Created).ThenBy(c => c.Title),
            LibraryItemOrderingOption.LastModifiedAsc => query.OrderBy(c => c.LastModified).ThenBy(c => c.Title),
            LibraryItemOrderingOption.LastListenedAsc or LibraryItemOrderingOption.LastListenedDesc => query.OrderByDescending(c => c.LastModified).ThenBy(c => c.Title),
            null or LibraryItemOrderingOption.LastModifiedDesc or _ => query.OrderByDescending(c => c.LastModified).ThenBy(c => c.Title)
        };
}
