using K7.Clients.Shared.Enums;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryBrowseFilters
{
    [Inject] private IStringLocalizer<LibraryGroup> LibraryGroupL { get; set; } = default!;

    [Parameter] public IReadOnlyList<MediaGenreDto> Genres { get; set; } = [];
    [Parameter] public IReadOnlySet<string> SelectedGenres { get; set; } = new HashSet<string>();
    [Parameter] public EventCallback<IReadOnlySet<string>> SelectedGenresChanged { get; set; }
    [Parameter] public LibraryBrowseWatchFilter WatchFilter { get; set; } = LibraryBrowseWatchFilter.All;
    [Parameter] public EventCallback<LibraryBrowseWatchFilter> WatchFilterChanged { get; set; }
    [Parameter] public bool ShowWatchFilters { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;

    private bool HasActiveFilters =>
        WatchFilter is not LibraryBrowseWatchFilter.All || SelectedGenres.Count > 0;

    private string ActiveFiltersLabel
    {
        get
        {
            var parts = new List<string>();

            if (WatchFilter is LibraryBrowseWatchFilter.Unwatched)
                parts.Add(L["WatchFilterUnwatched"]);
            else if (WatchFilter is LibraryBrowseWatchFilter.InProgress)
                parts.Add(L["WatchFilterInProgress"]);

            if (SelectedGenres.Count > 0)
                parts.Add(string.Join(", ", SelectedGenres.OrderBy(g => g, StringComparer.OrdinalIgnoreCase)));

            return parts.Count > 0 ? string.Join(" · ", parts) : L["Filters"];
        }
    }

    private string GetGenreLabel(MediaGenreDto genre) =>
        string.Format(LibraryGroupL["GenreFilterOption"], genre.Name, genre.MediaCount);

    private async Task SetWatchFilterAsync(LibraryBrowseWatchFilter value)
    {
        if (value == WatchFilter)
            return;

        await WatchFilterChanged.InvokeAsync(value);
    }

    private async Task ToggleGenreAsync(string genreName)
    {
        var next = new HashSet<string>(SelectedGenres, StringComparer.OrdinalIgnoreCase);
        if (!next.Add(genreName))
            next.Remove(genreName);

        await SelectedGenresChanged.InvokeAsync(next);
    }

    private async Task ClearFiltersAsync()
    {
        if (!HasActiveFilters)
            return;

        if (WatchFilter is not LibraryBrowseWatchFilter.All)
            await WatchFilterChanged.InvokeAsync(LibraryBrowseWatchFilter.All);

        if (SelectedGenres.Count > 0)
            await SelectedGenresChanged.InvokeAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}
