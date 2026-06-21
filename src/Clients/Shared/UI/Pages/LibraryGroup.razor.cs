using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace K7.Clients.Shared.UI.Pages;

public partial class LibraryGroup : IDisposable
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
    private IReadOnlyList<Guid>? _libraryIds;
    private List<MediaType> _availableMediaTypes = [];
    private List<ButtonGroupOption<MediaType>> _mediaTypeOptions = [];
    private MediaType _selectedMediaType;
    private MediaOrderingOption _selectedSort = MediaOrderingOption.TitleAsc;
    private HashSet<string> _selectedGenres = new(StringComparer.OrdinalIgnoreCase);
    private List<MediaGenreDto> _genres = [];
    private string? _activeSortKey = "title";
    private K7SortDirection _activeSortDirection = K7SortDirection.Ascending;

    private static readonly List<MediaOrderingOption> SortOptions =
    [
        MediaOrderingOption.TitleAsc,
        MediaOrderingOption.TitleDesc,
        MediaOrderingOption.CreatedDesc,
        MediaOrderingOption.CreatedAsc,
        MediaOrderingOption.ReleaseDateDesc,
        MediaOrderingOption.ReleaseDateAsc
    ];

    private float GridAspectRatio => _selectedMediaType is MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist ? 1f : 1.5f;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _selectedMediaType = default;

        var groupId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;

        if (groupId.HasValue)
        {
            var groups = await LibraryService.GetLibraryGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == groupId.Value);
            _libraryMediaType = group?.MediaType;
            _libraryIds = group?.LibraryIds;
        }

        _availableMediaTypes = _libraryMediaType switch
        {
            LibraryMediaType.Serie => [MediaType.Serie, MediaType.SerieSeason, MediaType.SerieEpisode],
            LibraryMediaType.Music => [MediaType.MusicArtist, MediaType.MusicAlbum, MediaType.MusicTrack],
            _ => []
        };

        _selectedMediaType = _libraryMediaType switch
        {
            LibraryMediaType.Movie => MediaType.Movie,
            LibraryMediaType.Serie => MediaType.Serie,
            LibraryMediaType.Music => MediaType.MusicArtist,
            _ => _availableMediaTypes.Count > 0 ? _availableMediaTypes[0] : default
        };

        _mediaTypeOptions = _availableMediaTypes
            .Select(mt => new ButtonGroupOption<MediaType>(mt, Label: GetMediaTypeLabel(mt)))
            .ToList();

        _selectedSort = MediaOrderingOption.TitleAsc;
        _activeSortKey = "title";
        _activeSortDirection = K7SortDirection.Ascending;
        _selectedGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _genres = [];

        K7HubClient.MediaBatchAdded += OnMediaBatchAdded;

        if (_libraryMediaType is LibraryMediaType.Movie or LibraryMediaType.Serie)
            await LoadGenresAsync();

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
                    LibraryIds = _libraryIds?.ToArray(),
                    MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
                    Genres = _selectedGenres.Count > 0 ? _selectedGenres.ToArray() : null,
                    OrderBy = orderBy is not null ? [orderBy.Value] : [_selectedSort],
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
        LibraryIds = _libraryIds?.ToArray(),
        MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
        Genres = _selectedGenres.Count > 0 ? _selectedGenres.ToArray() : null,
        OrderBy = [_selectedSort],
        PageNumber = pageNumber,
        PageSize = pageSize
    };

    private async Task LoadGenresAsync()
    {
        try
        {
            var result = await k7ServerService.GetMediaGenresAsync(new GetMediaGenresQuery
            {
                LibraryIds = _libraryIds?.ToArray(),
                MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
                OrderBy = [GenreOrderingOption.MediaCountDesc],
                PageNumber = 1,
                PageSize = 100
            });

            _genres = result?.Items?.ToList() ?? [];
        }
        catch
        {
            _genres = [];
        }
    }

    private async Task OnSelectedGenresChanged(IReadOnlySet<string> value)
    {
        _selectedGenres = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
        await RefreshAllAsync();
    }

    private async Task OnMediaTypeFilterChanged(MediaType value)
    {
        if (value == default || value == _selectedMediaType) return;

        _selectedMediaType = value;
        _selectedGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_libraryMediaType is LibraryMediaType.Movie or LibraryMediaType.Serie)
            await LoadGenresAsync();
        await RefreshAllAsync();
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

    private void OnColumnPickerRequested()
    {
        _dataTable?.ToggleColumnPicker();
    }

    private static string GetItemHref(LiteMediaDto item) => item switch
    {
        LiteMusicArtistDto artist => $"/music/artists/{artist.Id}",
        LiteMusicAlbumDto album => $"/music/albums/{album.Id}",
        LiteMusicTrackDto track => $"/music/albums/{track.AlbumId}",
        LiteSerieDto serie => $"/series/{serie.Id}",
        LiteSerieSeasonDto season => $"/series/{season.SerieId}/seasons/{season.SeasonNumber}",
        LiteSerieEpisodeDto ep => $"/series/{ep.SerieId}/seasons/{ep.SeasonNumber}#ep-{ep.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private static MediaCardVariant GetVariant(LiteMediaDto item) => item switch
    {
        LiteMusicAlbumDto or LiteMusicTrackDto or LiteMusicArtistDto => MediaCardVariant.Cover,
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
        MediaType.MusicArtist => L["Artists"],
        MediaType.MusicAlbum => L["Albums"],
        MediaType.MusicTrack => L["Tracks"],
        _ => mediaType.ToString()
    };

    private static readonly IReadOnlyList<string> AlphabetLabels =
        ["#", ..Enumerable.Range('A', 26).Select(c => ((char)c).ToString())];

    private IReadOnlyList<string>? JumpLabels => _selectedSort is MediaOrderingOption.TitleAsc or MediaOrderingOption.TitleDesc
        ? AlphabetLabels
        : null;

    private async Task OnJumpRequested(string label)
    {
        if (_browseView is null || _totalCount == 0) return;

        var index = await FindIndexForLetterAsync(label);
        _browseView.ScrollToItemIndex(index);
    }

    private async Task<int> FindIndexForLetterAsync(string label)
    {
        var ascending = _selectedSort is not MediaOrderingOption.TitleDesc;

        if (label == "#")
        {
            return ascending ? 0 : _totalCount - 1;
        }

        var targetChar = char.ToUpperInvariant(label[0]);
        var low = 0;
        var high = _totalCount - 1;
        var result = ascending ? _totalCount - 1 : 0;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var query = BuildQuery((mid / PageSize) + 1, PageSize);

            try
            {
                var page = await k7ServerService.GetLiteMediasAsync(query);
                var offset = mid - ((mid / PageSize) * PageSize);
                var items = page?.Items?.ToList();

                if (items is null || offset >= items.Count) break;

                var itemTitle = items[offset].Title ?? "";
                var itemChar = itemTitle.Length > 0 ? char.ToUpperInvariant(itemTitle[0]) : '#';
                var isLetter = char.IsLetter(itemChar);

                int cmp;
                if (!isLetter && targetChar == '#')
                {
                    cmp = 0;
                }
                else if (!isLetter)
                {
                    cmp = ascending ? -1 : 1;
                }
                else
                {
                    cmp = ascending
                        ? itemChar.CompareTo(targetChar)
                        : targetChar.CompareTo(itemChar);
                }

                if (cmp < 0)
                {
                    low = mid + 1;
                }
                else
                {
                    result = mid;
                    high = mid - 1;
                }
            }
            catch
            {
                break;
            }
        }

        return Math.Clamp(result, 0, Math.Max(0, _totalCount - 1));
    }

    public void Dispose()
    {
        K7HubClient.MediaBatchAdded -= OnMediaBatchAdded;
    }
}
