using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class Library
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter]
    public required string Id { get; set; }

    private List<MediaCardViewModel> MediaCards { get; set; } = [];
    private bool _loading = true;
    private LibraryMediaType? _libraryMediaType;
    private List<MediaType> _availableMediaTypes = [];
    private MediaType _selectedMediaType;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        MediaCards.Clear();
        _selectedMediaType = default;

        var libraryId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;

        if (libraryId.HasValue)
        {
            var libraries = await LibraryService.GetLibrariesAsync();
            var library = libraries.FirstOrDefault(l => l.Id == libraryId.Value);
            _libraryMediaType = library?.MediaType;
        }

        _availableMediaTypes = _libraryMediaType switch
        {
            LibraryMediaType.Serie => [MediaType.Serie, MediaType.SerieSeason, MediaType.SerieEpisode],
            LibraryMediaType.Music => [MediaType.MusicAlbum, MediaType.MusicTrack],
            _ => []
        };

        _selectedMediaType = _availableMediaTypes.Count > 0
            ? _availableMediaTypes[0]
            : default;

        HashSet<MediaType>? initialFilter = _selectedMediaType != default
            ? [_selectedMediaType]
            : null;

        await LoadMediasAsync(libraryId, initialFilter);
    }

    private async Task LoadMediasAsync(Guid? libraryId, HashSet<MediaType>? mediaTypes = null)
    {
        _loading = true;
        MediaCards.Clear();

        var liteMediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
        {
            LibraryIds = libraryId.HasValue ? [libraryId.Value] : null,
            MediaTypes = mediaTypes,
            PageNumber = 1,
            PageSize = 1000
        });

        if (liteMediasPage != null && liteMediasPage.Items?.Count != 0)
        {
            foreach (var item in liteMediasPage.Items!)
            {
                if (item.ToCardViewModel(apiClient) is { } vm)
                    MediaCards.Add(vm);
            }
        }

        _loading = false;
    }

    private async Task OnMediaTypeFilterChanged(MediaType value)
    {
        if (value == default) return;

        _selectedMediaType = value;

        var libraryId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;
        await LoadMediasAsync(libraryId, [value]);
    }

    private string GetMediaTypeLabel(MediaType mediaType) => mediaType switch
    {
        MediaType.Movie => S["MediaTypeMovies"],
        MediaType.Serie => S["MediaTypeSeries"],
        MediaType.SerieSeason => L["Seasons"],
        MediaType.SerieEpisode => L["Episodes"],
        MediaType.MusicAlbum => L["Albums"],
        MediaType.MusicTrack => L["Tracks"],
        _ => mediaType.ToString()
    };

    private void NavigateToItem(MediaCardViewModel item)
    {
        Navigation.NavigateTo(GetItemHref(item));
    }

    private void OnRowKeyDown(KeyboardEventArgs e, MediaCardViewModel item)
    {
        if (e.Code is "Enter" or "Space")
        {
            NavigateToItem(item);
        }
    }

    private static string GetItemHref(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => $"/music/albums/{item.ParentId ?? item.Id}",
        MediaCardKind.Serie => $"/series/{item.Id}",
        MediaCardKind.Season => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}",
        MediaCardKind.Episode => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}#ep-{item.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private static MediaCardVariant GetVariant(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => MediaCardVariant.Cover,
        _ => MediaCardVariant.Poster
    };
}
