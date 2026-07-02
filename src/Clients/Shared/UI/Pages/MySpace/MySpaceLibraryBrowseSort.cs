using K7.Clients.Shared.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.MySpace;

internal static class MySpaceLibraryBrowseSort
{
    internal static readonly List<LibraryItemOrderingOption> CollectionOptions =
    [
        LibraryItemOrderingOption.LastModifiedDesc,
        LibraryItemOrderingOption.TitleAsc,
        LibraryItemOrderingOption.TitleDesc,
        LibraryItemOrderingOption.CreatedDesc,
        LibraryItemOrderingOption.CreatedAsc,
        LibraryItemOrderingOption.LastModifiedAsc
    ];

    internal static readonly List<LibraryItemOrderingOption> PlaylistOptions =
    [
        LibraryItemOrderingOption.LastListenedDesc,
        LibraryItemOrderingOption.TitleAsc,
        LibraryItemOrderingOption.TitleDesc,
        LibraryItemOrderingOption.CreatedDesc,
        LibraryItemOrderingOption.CreatedAsc,
        LibraryItemOrderingOption.LastListenedAsc
    ];

    internal static string GetLabel(LibraryItemOrderingOption option, IStringLocalizer<LibraryGroup> libraryL) =>
        option switch
        {
            LibraryItemOrderingOption.TitleAsc => libraryL["SortTitleAsc"],
            LibraryItemOrderingOption.TitleDesc => libraryL["SortTitleDesc"],
            LibraryItemOrderingOption.CreatedDesc => libraryL["SortNewest"],
            LibraryItemOrderingOption.CreatedAsc => libraryL["SortOldest"],
            LibraryItemOrderingOption.LastModifiedDesc => libraryL["SortRecentActivity"],
            LibraryItemOrderingOption.LastModifiedAsc => libraryL["SortOldestActivity"],
            LibraryItemOrderingOption.LastListenedDesc => libraryL["SortLastListenedRecent"],
            LibraryItemOrderingOption.LastListenedAsc => libraryL["SortLastListenedOldest"],
            _ => libraryL["SortRecentActivity"]
        };

    internal static (string? Key, K7SortDirection Direction) MapPlaylistOrderingToSortKey(LibraryItemOrderingOption option) =>
        option switch
        {
            LibraryItemOrderingOption.TitleAsc => ("title", K7SortDirection.Ascending),
            LibraryItemOrderingOption.TitleDesc => ("title", K7SortDirection.Descending),
            LibraryItemOrderingOption.CreatedAsc => ("created", K7SortDirection.Ascending),
            LibraryItemOrderingOption.CreatedDesc => ("created", K7SortDirection.Descending),
            LibraryItemOrderingOption.LastListenedAsc => ("lastListened", K7SortDirection.Ascending),
            LibraryItemOrderingOption.LastListenedDesc => ("lastListened", K7SortDirection.Descending),
            _ => ("lastListened", K7SortDirection.Descending)
        };

    internal static LibraryItemOrderingOption? MapSortKeyToPlaylistOrdering(string? sortKey, K7SortDirection direction) =>
        (sortKey, direction) switch
        {
            ("title", K7SortDirection.Ascending) => LibraryItemOrderingOption.TitleAsc,
            ("title", K7SortDirection.Descending) => LibraryItemOrderingOption.TitleDesc,
            ("created", K7SortDirection.Ascending) => LibraryItemOrderingOption.CreatedAsc,
            ("created", K7SortDirection.Descending) => LibraryItemOrderingOption.CreatedDesc,
            ("lastListened", K7SortDirection.Ascending) => LibraryItemOrderingOption.LastListenedAsc,
            ("lastListened", K7SortDirection.Descending) => LibraryItemOrderingOption.LastListenedDesc,
            _ => null
        };

    internal static (string? Key, K7SortDirection Direction) MapCollectionOrderingToSortKey(LibraryItemOrderingOption option) =>
        option switch
        {
            LibraryItemOrderingOption.TitleAsc => ("title", K7SortDirection.Ascending),
            LibraryItemOrderingOption.TitleDesc => ("title", K7SortDirection.Descending),
            LibraryItemOrderingOption.CreatedAsc => ("created", K7SortDirection.Ascending),
            LibraryItemOrderingOption.CreatedDesc => ("created", K7SortDirection.Descending),
            LibraryItemOrderingOption.LastModifiedAsc => ("lastModified", K7SortDirection.Ascending),
            LibraryItemOrderingOption.LastModifiedDesc => ("lastModified", K7SortDirection.Descending),
            _ => ("lastModified", K7SortDirection.Descending)
        };

    internal static LibraryItemOrderingOption? MapSortKeyToCollectionOrdering(string? sortKey, K7SortDirection direction) =>
        (sortKey, direction) switch
        {
            ("title", K7SortDirection.Ascending) => LibraryItemOrderingOption.TitleAsc,
            ("title", K7SortDirection.Descending) => LibraryItemOrderingOption.TitleDesc,
            ("created", K7SortDirection.Ascending) => LibraryItemOrderingOption.CreatedAsc,
            ("created", K7SortDirection.Descending) => LibraryItemOrderingOption.CreatedDesc,
            ("lastModified", K7SortDirection.Ascending) => LibraryItemOrderingOption.LastModifiedAsc,
            ("lastModified", K7SortDirection.Descending) => LibraryItemOrderingOption.LastModifiedDesc,
            _ => null
        };
}
