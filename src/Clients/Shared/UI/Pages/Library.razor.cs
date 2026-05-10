using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class Library : IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private MediaCacheStore CacheStore { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;

    [Parameter]
    public required string Id { get; set; }

    private List<MediaCardViewModel> MediaCards { get; set; } = [];
    private bool _loading = true;
    private bool _hasMore;
    private int _currentPage = 1;
    private int _totalCount;
    private const int PageSize = 50;
    private LibraryMediaType? _libraryMediaType;
    private List<MediaType> _availableMediaTypes = [];
    private MediaType _selectedMediaType;
    private string? _searchText;
    private MediaOrderingOption _selectedSort = MediaOrderingOption.TitleAsc;
    private Timer? _searchDebounce;

    private static readonly List<MediaOrderingOption> SortOptions =
    [
        MediaOrderingOption.TitleAsc,
        MediaOrderingOption.TitleDesc,
        MediaOrderingOption.CreatedDesc,
        MediaOrderingOption.CreatedAsc,
        MediaOrderingOption.ReleaseDateDesc,
        MediaOrderingOption.ReleaseDateAsc
    ];

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _currentPage = 1;
        _searchText = null;
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

        _selectedMediaType = _libraryMediaType switch
        {
            LibraryMediaType.Movie => MediaType.Movie,
            LibraryMediaType.Serie => MediaType.Serie,
            LibraryMediaType.Music => MediaType.MusicAlbum,
            _ => _availableMediaTypes.Count > 0 ? _availableMediaTypes[0] : default
        };

        _selectedSort = MediaOrderingOption.TitleAsc;

        K7HubClient.MediaBatchAdded += OnMediaBatchAdded;

        await LoadMediasAsync();
    }

    private async Task LoadMediasAsync(bool append = false)
    {
        if (!append)
        {
            _loading = true;
            _currentPage = 1;
            MediaCards.Clear();
        }

        var libraryId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;
        HashSet<MediaType>? mediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null;

        var query = new GetMediasWithPaginationQuery
        {
            LibraryIds = libraryId.HasValue ? [libraryId.Value] : null,
            MediaTypes = mediaTypes,
            OrderBy = [_selectedSort],
            SearchText = _searchText,
            PageNumber = _currentPage,
            PageSize = PageSize
        };

        var cacheKey = MediaCacheStore.BuildKey("library", Id, _selectedMediaType.ToString(), _selectedSort.ToString(), _searchText, _currentPage.ToString());

        if (!append)
        {
            var cached = CacheStore.Get<List<MediaCardViewModel>>(cacheKey);
            if (cached is not null)
            {
                MediaCards.AddRange(cached);
                _loading = false;
                _ = Task.Run(async () => await RefreshLibraryInBackground(query, cacheKey));
                return;
            }
        }

        var liteMediasPage = await k7ServerService.GetLiteMediasAsync(query);

        if (liteMediasPage?.Items is { Count: > 0 })
        {
            _totalCount = liteMediasPage.TotalCount ?? 0;
            var newItems = new List<MediaCardViewModel>();
            foreach (var item in liteMediasPage.Items)
            {
                if (item.ToCardViewModel(apiClient, n => string.Format(S["SeasonNumber"], n)) is { } vm)
                    newItems.Add(vm);
            }

            MediaCards.AddRange(newItems);
            _hasMore = MediaCards.Count < _totalCount;

            if (!append)
            {
                CacheStore.Set(cacheKey, newItems);
            }
        }
        else
        {
            _hasMore = false;
            if (!append)
            {
                _totalCount = 0;
            }
        }

        _loading = false;
    }

    private async Task RefreshLibraryInBackground(GetMediasWithPaginationQuery query, string cacheKey)
    {
        var liteMediasPage = await k7ServerService.GetLiteMediasAsync(query);
        if (liteMediasPage?.Items is null) return;

        var items = new List<MediaCardViewModel>();
        foreach (var item in liteMediasPage.Items)
        {
            if (item.ToCardViewModel(apiClient, n => string.Format(S["SeasonNumber"], n)) is { } vm)
                items.Add(vm);
        }

        CacheStore.Set(cacheKey, items);
        _totalCount = liteMediasPage.TotalCount ?? 0;

        await InvokeAsync(() =>
        {
            MediaCards.Clear();
            MediaCards.AddRange(items);
            _hasMore = MediaCards.Count < _totalCount;
            StateHasChanged();
        });
    }

    private async Task LoadMoreAsync()
    {
        if (!_hasMore) return;
        _currentPage++;
        await LoadMediasAsync(append: true);
    }

    private async Task OnMediaTypeFilterChanged(MediaType value)
    {
        if (value == default || value == _selectedMediaType) return;

        _selectedMediaType = value;
        _searchText = null;
        await LoadMediasAsync();
    }

    private void OnSearchTextChanged(string? value)
    {
        _searchDebounce?.Dispose();
        _searchDebounce = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                _searchText = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                await LoadMediasAsync();
                StateHasChanged();
            });
        }, null, 300, Timeout.Infinite);
    }

    private async Task OnSortChanged(MediaOrderingOption value)
    {
        if (value == _selectedSort) return;
        _selectedSort = value;
        await LoadMediasAsync();
    }

    private void OnMediaBatchAdded(List<MediaBatchItem> items)
    {
        CacheStore.InvalidateByPrefix($"library:{Id}");
        _ = InvokeAsync(async () =>
        {
            await LoadMediasAsync();
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        K7HubClient.MediaBatchAdded -= OnMediaBatchAdded;
        _searchDebounce?.Dispose();
    }

    private string GetSortLabel(MediaOrderingOption option) => option switch
    {
        MediaOrderingOption.TitleAsc => L["SortTitleAsc"],
        MediaOrderingOption.TitleDesc => L["SortTitleDesc"],
        MediaOrderingOption.CreatedDesc => L["SortNewest"],
        MediaOrderingOption.CreatedAsc => L["SortOldest"],
        MediaOrderingOption.ReleaseDateDesc => L["SortReleaseDateDesc"],
        MediaOrderingOption.ReleaseDateAsc => L["SortReleaseDateAsc"],
        _ => option.ToString()
    };

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

    private static string GetItemHref(MediaCardViewModel item) => item.NavigationTarget ?? item.Kind switch
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
