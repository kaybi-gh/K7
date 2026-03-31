using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
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
    private bool IsAllSelected => _selectedMediaType == default;

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

        await LoadMediasAsync(libraryId);
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
        _selectedMediaType = value;

        var libraryId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;
        HashSet<MediaType>? filter = value == default ? null : [value];

        await LoadMediasAsync(libraryId, filter);
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
        MediaCardKind.Season => $"/series/{item.ParentId ?? item.Id}",
        MediaCardKind.Episode => $"/series/{item.ParentId ?? item.Id}",
        _ => $"/movies/{item.Id}"
    };
}
