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
    [Parameter] public string Class { get; set; } = string.Empty;

    private string ActiveFiltersLabel =>
        SelectedGenres.Count > 0
            ? string.Join(", ", SelectedGenres.OrderBy(g => g, StringComparer.OrdinalIgnoreCase))
            : L["Filters"];

    private string GetGenreLabel(MediaGenreDto genre) =>
        string.Format(LibraryGroupL["GenreFilterOption"], genre.Name, genre.MediaCount);

    private async Task ToggleGenreAsync(string genreName)
    {
        var next = new HashSet<string>(SelectedGenres, StringComparer.OrdinalIgnoreCase);
        if (!next.Add(genreName))
            next.Remove(genreName);

        await SelectedGenresChanged.InvokeAsync(next);
    }

    private async Task ClearFiltersAsync()
    {
        if (SelectedGenres.Count == 0)
            return;

        await SelectedGenresChanged.InvokeAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }
}
