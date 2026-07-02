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
}
