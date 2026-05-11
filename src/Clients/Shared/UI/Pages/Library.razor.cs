using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace K7.Clients.Shared.UI.Pages;

public partial class Library : IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;

    [Parameter]
    public required string Id { get; set; }

    private BrowseView<LiteMediaDto>? _browseView;
    private K7DataTable<LiteMediaDto>? _dataTable;
    private bool _loading = true;
    private int _totalCount;
    private const int PageSize = 50;
    private LibraryMediaType? _libraryMediaType;
    private List<MediaType> _availableMediaTypes = [];
    private MediaType _selectedMediaType;
    private string? _searchText;
    private MediaOrderingOption _selectedSort = MediaOrderingOption.TitleAsc;
    private string? _activeSortKey = "title";
    private K7SortDirection _activeSortDirection = K7SortDirection.Ascending;
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
        _searchText = null;
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
        _activeSortKey = "title";
        _activeSortDirection = K7SortDirection.Ascending;

        K7HubClient.MediaBatchAdded += OnMediaBatchAdded;

        // Initial load to get total count
        var query = BuildQuery(1, PageSize);
        var result = await k7ServerService.GetLiteMediasAsync(query);
        _totalCount = result?.TotalCount ?? 0;
        _loading = false;
    }

    private async ValueTask<ItemsProviderResult<LiteMediaDto>> ProvideMediasAsync(
        ItemsProviderRequest request)
    {
        try
        {
            var startIndex = request.StartIndex;
            var count = request.Count;

            var firstPage = (startIndex / PageSize) + 1;
            var lastPage = ((startIndex + count - 1) / PageSize) + 1;

            var pages = Enumerable.Range(firstPage, lastPage - firstPage + 1);
            var tasks = pages.Select(page =>
                k7ServerService.GetLiteMediasAsync(
                    BuildQuery(page, PageSize), request.CancellationToken));

            var results = await Task.WhenAll(tasks);

            var allItems = new List<LiteMediaDto>(count);
            foreach (var result in results)
            {
                if (result?.Items is { Count: > 0 })
                {
                    _totalCount = result.TotalCount ?? 0;
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();

            if (items.Count > 0)
            {
                await InvokeAsync(StateHasChanged);
            }

            return new ItemsProviderResult<LiteMediaDto>(items, _totalCount);
        }
        catch (OperationCanceledException)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            return default;
        }
    }

    private async Task<K7DataTableResult<LiteMediaDto>> LoadTableDataAsync(
        K7DataTableState<LiteMediaDto> state, CancellationToken cancellationToken)
    {
        try
        {
            var startIndex = state.StartIndex;
            var count = state.Count;
            var orderBy = MapSortKeyToOrdering(state.SortKey, state.SortDirection);

            var firstPage = (startIndex / PageSize) + 1;
            var lastPage = ((startIndex + count - 1) / PageSize) + 1;

            var pages = Enumerable.Range(firstPage, lastPage - firstPage + 1);
            var tasks = pages.Select(page =>
            {
                var query = new GetMediasWithPaginationQuery
                {
                    LibraryIds = Guid.TryParse(Id, out var parsed) ? [parsed] : null,
                    MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
                    OrderBy = orderBy is not null ? [orderBy.Value] : [_selectedSort],
                    SearchText = _searchText,
                    PageNumber = page,
                    PageSize = PageSize
                };
                return k7ServerService.GetLiteMediasAsync(query, cancellationToken);
            });

            var results = await Task.WhenAll(tasks);

            var allItems = new List<LiteMediaDto>(count);
            foreach (var result in results)
            {
                if (result?.Items is { Count: > 0 })
                {
                    _totalCount = result.TotalCount ?? 0;
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();

            if (items.Count > 0)
            {
                await InvokeAsync(StateHasChanged);
            }

            return new K7DataTableResult<LiteMediaDto>(items, _totalCount);
        }
        catch (OperationCanceledException)
        {
            return new K7DataTableResult<LiteMediaDto>([], _totalCount);
        }
    }

    private GetMediasWithPaginationQuery BuildQuery(int pageNumber, int pageSize) => new()
    {
        LibraryIds = Guid.TryParse(Id, out var parsed) ? [parsed] : null,
        MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
        OrderBy = [_selectedSort],
        SearchText = _searchText,
        PageNumber = pageNumber,
        PageSize = pageSize
    };

    private async Task OnMediaTypeFilterChanged(MediaType value)
    {
        if (value == default || value == _selectedMediaType) return;

        _selectedMediaType = value;
        _searchText = null;
        await RefreshAllAsync();
    }

    private void OnSearchTextChanged(string? value)
    {
        _searchDebounce?.Dispose();
        _searchDebounce = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                _searchText = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                await RefreshAllAsync();
                StateHasChanged();
            });
        }, null, 300, Timeout.Infinite);
    }

    private async Task OnSortChanged(MediaOrderingOption value)
    {
        if (value == _selectedSort) return;
        _selectedSort = value;

        // Sync sort key/direction from dropdown
        (_activeSortKey, _activeSortDirection) = MapOrderingToSortKey(value);

        await RefreshAllAsync();
    }

    private async Task OnTableSortChanged(SortChangedEventArgs args)
    {
        _activeSortKey = args.SortKey;
        _activeSortDirection = args.Direction;

        var ordering = MapSortKeyToOrdering(args.SortKey, args.Direction);
        if (ordering is not null)
        {
            _selectedSort = ordering.Value;
        }

        // Table refreshes itself; refresh grid/list too if they share the provider
        if (_browseView is not null)
        {
            await _browseView.RefreshAsync();
        }
    }

    private void OnMediaBatchAdded(List<MediaBatchItem> items)
    {
        _ = InvokeAsync(async () =>
        {
            await RefreshAllAsync();
            StateHasChanged();
        });
    }

    private async Task RefreshAllAsync()
    {
        if (_browseView is not null)
        {
            await _browseView.RefreshAsync();
        }

        if (_dataTable is not null)
        {
            await _dataTable.RefreshAsync();
        }
    }

    private void NavigateToItem(LiteMediaDto item)
    {
        Navigation.NavigateTo(GetItemHref(item));
    }

    private static string GetItemHref(LiteMediaDto item) => item switch
    {
        LiteMusicAlbumDto album => $"/music/albums/{album.Id}",
        LiteMusicTrackDto track => $"/music/albums/{track.AlbumId}",
        LiteSerieDto serie => $"/series/{serie.Id}",
        LiteSerieSeasonDto season => $"/series/{season.SerieId}/seasons/{season.SeasonNumber}",
        LiteSerieEpisodeDto ep => $"/series/{ep.SerieId}/seasons/{ep.SeasonNumber}#ep-{ep.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private static MediaCardVariant GetVariant(LiteMediaDto item) => item switch
    {
        LiteMusicAlbumDto or LiteMusicTrackDto => MediaCardVariant.Cover,
        _ => MediaCardVariant.Poster
    };

    private static MediaOrderingOption? MapSortKeyToOrdering(string? sortKey, K7SortDirection direction) =>
        (sortKey, direction) switch
        {
            ("title", K7SortDirection.Ascending) => MediaOrderingOption.TitleAsc,
            ("title", K7SortDirection.Descending) => MediaOrderingOption.TitleDesc,
            ("releaseDate", K7SortDirection.Ascending) => MediaOrderingOption.ReleaseDateAsc,
            ("releaseDate", K7SortDirection.Descending) => MediaOrderingOption.ReleaseDateDesc,
            ("created", K7SortDirection.Ascending) => MediaOrderingOption.CreatedAsc,
            ("created", K7SortDirection.Descending) => MediaOrderingOption.CreatedDesc,
            ("localRating", K7SortDirection.Ascending) => MediaOrderingOption.LocalRatingAsc,
            ("localRating", K7SortDirection.Descending) => MediaOrderingOption.LocalRatingDesc,
            ("playCount", K7SortDirection.Ascending) => MediaOrderingOption.PlayCountAsc,
            ("playCount", K7SortDirection.Descending) => MediaOrderingOption.PlayCountDesc,
            ("lastInteracted", K7SortDirection.Ascending) => MediaOrderingOption.LastInteractedAsc,
            ("lastInteracted", K7SortDirection.Descending) => MediaOrderingOption.LastInteractedDesc,
            _ => null
        };

    private static (string? Key, K7SortDirection Direction) MapOrderingToSortKey(MediaOrderingOption option) =>
        option switch
        {
            MediaOrderingOption.TitleAsc => ("title", K7SortDirection.Ascending),
            MediaOrderingOption.TitleDesc => ("title", K7SortDirection.Descending),
            MediaOrderingOption.ReleaseDateAsc => ("releaseDate", K7SortDirection.Ascending),
            MediaOrderingOption.ReleaseDateDesc => ("releaseDate", K7SortDirection.Descending),
            MediaOrderingOption.CreatedAsc => ("created", K7SortDirection.Ascending),
            MediaOrderingOption.CreatedDesc => ("created", K7SortDirection.Descending),
            _ => ("title", K7SortDirection.Ascending)
        };

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

    private static object? FormatLastInteracted(LiteMediaDto item) =>
        item.UserState?.LastInteractedAt?.ToString("d");

    public void Dispose()
    {
        K7HubClient.MediaBatchAdded -= OnMediaBatchAdded;
        _searchDebounce?.Dispose();
    }
}
