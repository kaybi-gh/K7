using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Notifications;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;
using K7.Shared.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using System.Text.Json;

namespace K7.Clients.Shared.UI.Pages;

public partial class LibraryGroup : IDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IMusicIntelligenceClientService MusicIntelligence { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Parameter]
    public required string Id { get; set; }

    [SupplyParameterFromQuery(Name = "genre")]
    public string? GenreQuery { get; set; }

    [SupplyParameterFromQuery(Name = "studio")]
    public string? StudioQuery { get; set; }

    [SupplyParameterFromQuery(Name = "network")]
    public string? NetworkQuery { get; set; }

    [SupplyParameterFromQuery(Name = "mediaType")]
    public string? MediaTypeQuery { get; set; }

    private BrowseView<LiteMediaDto>? _browseView;
    private K7DataTable<LiteMediaDto>? _dataTable;
    private bool _loading = true;
    private bool _canSetWatchState;
    private bool _canExclude;
    private bool _isAdmin;
    private int _totalCount;
    private const int PageSize = 50;
    private LibraryMediaType? _libraryMediaType;
    private IReadOnlyList<Guid>? _libraryIds;
    private Guid[]? _libraryGroupIds;
    private List<MediaType> _availableMediaTypes = [];
    private List<ButtonGroupOption<MediaType>> _mediaTypeOptions = [];
    private MediaType _selectedMediaType;
    private MediaOrderingOption _selectedSort = MediaOrderingOption.TitleAsc;
    private RuleGroupDto _filter = MediaBrowseFilterPresets.Empty;
    private IntelligentSearchRequest? _intelligentSearch;
    private List<LiteMediaDto> _intelligentSearchResults = [];
    private bool _intelligentSearchLoading;
    private MediaTagsDto? _tags;
    private bool _showWatchFilters =>
        _selectedMediaType is MediaType.Movie or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    private bool _showMusicPlaybackActions =>
        _libraryMediaType == LibraryMediaType.Music
        && (_intelligentSearch is not null || _selectedMediaType == MediaType.MusicTrack);

    private bool _canPlayMusic =>
        _totalCount > 0 && !_loading && !_intelligentSearchLoading;
    private string? _activeSortKey = "title";
    private K7SortDirection _activeSortDirection = K7SortDirection.Ascending;
    private string _tableScopeKey = "initial";

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

    private string FilterStorageKey => $"library-group.{Id}";

    private bool HasQuerySeededFilters =>
        !string.IsNullOrWhiteSpace(GenreQuery)
        || !string.IsNullOrWhiteSpace(StudioQuery)
        || !string.IsNullOrWhiteSpace(NetworkQuery)
        || !string.IsNullOrWhiteSpace(MediaTypeQuery);

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _selectedMediaType = default;
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);
        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);

        var groupId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;

        if (groupId.HasValue)
        {
            var groups = await LibraryService.GetLibraryGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == groupId.Value);
            _libraryMediaType = group?.MediaType;
            _libraryIds = group?.LibraryIds;
            _libraryGroupIds = [groupId.Value];
        }
        else
        {
            _libraryGroupIds = null;
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

        if (!string.IsNullOrWhiteSpace(MediaTypeQuery)
            && Enum.TryParse<MediaType>(MediaTypeQuery, ignoreCase: true, out var parsedMediaType)
            && (_availableMediaTypes.Contains(parsedMediaType)
                || (_availableMediaTypes.Count == 0 && parsedMediaType == _selectedMediaType)))
        {
            _selectedMediaType = parsedMediaType;
        }

        _mediaTypeOptions = _availableMediaTypes
            .Select(mt => new ButtonGroupOption<MediaType>(mt, Label: GetMediaTypeLabel(mt)))
            .ToList();

        _selectedSort = MediaOrderingOption.TitleAsc;
        _activeSortKey = "title";
        _activeSortDirection = K7SortDirection.Ascending;
        _filter = MediaBrowseFilterPresets.Empty;
        if (!string.IsNullOrWhiteSpace(GenreQuery))
            _filter = MediaBrowseFilterPresets.ToggleGenre(_filter, GenreQuery);
        if (!string.IsNullOrWhiteSpace(StudioQuery))
            _filter = MediaBrowseFilterPresets.SetSearchFieldValue(_filter, "Studio", StudioQuery);
        if (!string.IsNullOrWhiteSpace(NetworkQuery))
            _filter = MediaBrowseFilterPresets.SetSearchFieldValue(_filter, "Network", NetworkQuery);

        _intelligentSearch = null;
        _intelligentSearchResults = [];

        if (!HasQuerySeededFilters)
        {
            await LoadPersistedFiltersAsync();
        }

        K7HubClient.MediaBatchAdded += OnMediaBatchAdded;

        await LoadTagsAsync();

        // Initial load to get total count
        if (_intelligentSearch is not null)
        {
            await OnIntelligentSearchChanged(_intelligentSearch);
        }
        else
        {
            var result = await k7ServerService.QueryMediasAsync(BuildQuery(1, PageSize));
            _totalCount = result?.TotalCount ?? 0;
        }

        _loading = false;
    }

    private async ValueTask<ItemsProviderResult<LiteMediaDto>> ProvideMediasAsync(
        ItemsProviderRequest request)
    {
        if (_intelligentSearch is not null)
            return ProvideIntelligentSearchMedias(request);

        try
        {
            var startIndex = request.StartIndex;
            var count = request.Count;

            var firstPage = (startIndex / PageSize) + 1;
            var lastPage = ((startIndex + count - 1) / PageSize) + 1;

            var pages = Enumerable.Range(firstPage, lastPage - firstPage + 1);
            var tasks = pages.Select(page =>
                k7ServerService.QueryMediasAsync(
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
        if (_intelligentSearch is not null)
        {
            var items = _intelligentSearchResults
                .Skip(state.StartIndex)
                .Take(state.Count)
                .ToList();
            return new K7DataTableResult<LiteMediaDto>(items, _intelligentSearchResults.Count);
        }

        try
        {
            var startIndex = state.StartIndex;
            var count = state.Count;
            var orderBy = MapSortKeyToOrdering(state.SortKey, state.SortDirection);

            var firstPage = (startIndex / PageSize) + 1;
            var lastPage = ((startIndex + count - 1) / PageSize) + 1;

            var pages = Enumerable.Range(firstPage, lastPage - firstPage + 1);
            var tasks = pages.Select(page =>
                k7ServerService.QueryMediasAsync(
                    BuildQuery(page, PageSize, orderBy), cancellationToken));

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

    private QueryMediasRequest BuildQuery(
        int pageNumber,
        int pageSize,
        MediaOrderingOption? orderBy = null) => new()
    {
        LibraryIds = _libraryIds?.ToArray(),
        MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
        Filter = _filter,
        OrderBy = orderBy is not null ? [orderBy.Value] : [_selectedSort],
        PageNumber = pageNumber,
        PageSize = pageSize
    };

    private async Task LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<LibraryGroupFilterState>(FilterStorageKey);
            if (state is null)
            {
                return;
            }

            if (Enum.IsDefined(typeof(MediaType), state.MediaType)
                && (_availableMediaTypes.Count == 0 || _availableMediaTypes.Contains((MediaType)state.MediaType)))
            {
                _selectedMediaType = (MediaType)state.MediaType;
            }

            if (Enum.IsDefined(typeof(MediaOrderingOption), state.Sort))
            {
                _selectedSort = (MediaOrderingOption)state.Sort;
                (_activeSortKey, _activeSortDirection) = MapOrderingToSortKey(_selectedSort);
            }

            if (!string.IsNullOrWhiteSpace(state.FilterJson))
            {
                _filter = JsonSerializer.Deserialize<RuleGroupDto>(state.FilterJson) ?? MediaBrowseFilterPresets.Empty;
            }

            if (!string.IsNullOrWhiteSpace(state.IntelligentSearchJson))
            {
                _intelligentSearch = JsonSerializer.Deserialize<IntelligentSearchRequest>(state.IntelligentSearchJson);
            }
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task PersistFiltersAsync()
    {
        try
        {
            var state = new LibraryGroupFilterState(
                (int)_selectedMediaType,
                (int)_selectedSort,
                MediaBrowseFilterPresets.IsEmpty(_filter) ? null : JsonSerializer.Serialize(_filter),
                _intelligentSearch is null ? null : JsonSerializer.Serialize(_intelligentSearch));
            await PageFilterStorage.SaveAsync(FilterStorageKey, state);
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task LoadTagsAsync()
    {
        try
        {
            _tags = await k7ServerService.GetMediaTagsAsync(new GetMediaTagsQuery
            {
                LibraryIds = _libraryIds?.ToArray(),
                LibraryGroupIds = _libraryGroupIds,
                MediaTypes = _selectedMediaType != default ? [_selectedMediaType] : null,
                Kinds =
                [
                    MetadataTagKind.Genre,
                    MetadataTagKind.ContentRating,
                    MetadataTagKind.Studio,
                    MetadataTagKind.Network
                ],
                OrderBy = [MediaTagOrderingOption.MediaCountDesc],
                PageNumber = 1,
                PageSize = 100
            });

        }
        catch
        {
            _tags = null;
        }
    }

    private async Task OnFilterChanged(RuleGroupDto value)
    {
        _filter = value;
        if (_intelligentSearch is not null)
        {
            _intelligentSearch = null;
            _intelligentSearchResults = [];
        }

        await PersistFiltersAsync();
        await RefreshAllAsync();
    }

    private async Task OnIntelligentSearchChanged(IntelligentSearchRequest? value)
    {
        _intelligentSearch = value;
        _filter = MediaBrowseFilterPresets.Empty;

        if (value is null)
        {
            _intelligentSearchResults = [];
            _totalCount = 0;
            await PersistFiltersAsync();
            await RefreshAllAsync();
            return;
        }

        if (_libraryMediaType == LibraryMediaType.Music)
            _selectedMediaType = MediaType.MusicTrack;

        _intelligentSearchLoading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var trackIds = await IntelligentSearchHelper.SearchTrackIdsAsync(MusicIntelligence, value);
            if (trackIds.Count == 0)
            {
                Snackbar.Add(L["IntelligentSearchNoResults"], K7Severity.Info);
                _intelligentSearchResults = [];
                _totalCount = 0;
                return;
            }

            var tracks = await IntelligentSearchHelper.LoadScopedTracksAsync(
                k7ServerService,
                trackIds,
                _libraryIds?.ToArray(),
                _libraryGroupIds);

            _intelligentSearchResults = tracks.Cast<LiteMediaDto>().ToList();
            _totalCount = _intelligentSearchResults.Count;
        }
        catch
        {
            Snackbar.Add(L["IntelligentSearchError"], K7Severity.Error);
            _intelligentSearchResults = [];
            _totalCount = 0;
        }
        finally
        {
            _intelligentSearchLoading = false;
            await PersistFiltersAsync();
            await RefreshAllAsync();
        }
    }

    private ItemsProviderResult<LiteMediaDto> ProvideIntelligentSearchMedias(ItemsProviderRequest request)
    {
        var items = _intelligentSearchResults
            .Skip(request.StartIndex)
            .Take(request.Count)
            .ToList();

        return new ItemsProviderResult<LiteMediaDto>(items, _intelligentSearchResults.Count);
    }

    private async Task PlayAllAsync()
    {
        var tracks = await GetPlayableTracksAsync();
        var queueItems = IntelligentSearchHelper.ToQueueItems(tracks, S["Untitled"]);
        if (queueItems.Count > 0)
            await Audio.PlayTracksAsync(queueItems, 0);
    }

    private async Task ShuffleAllAsync()
    {
        var tracks = await GetPlayableTracksAsync();
        var queueItems = IntelligentSearchHelper.ToQueueItems(tracks, S["Untitled"]);
        if (queueItems.Count == 0)
            return;

        if (!Audio.Shuffle)
            Audio.ToggleShuffle();

        await Audio.PlayTracksAsync(queueItems, 0);
    }

    private async Task<List<LiteMusicTrackDto>> GetPlayableTracksAsync(CancellationToken cancellationToken = default)
    {
        if (_intelligentSearch is not null)
            return _intelligentSearchResults.OfType<LiteMusicTrackDto>().ToList();

        if (_selectedMediaType != MediaType.MusicTrack || _totalCount == 0)
            return [];

        var tracks = new List<LiteMusicTrackDto>(_totalCount);
        var totalPages = (_totalCount + PageSize - 1) / PageSize;

        for (var page = 1; page <= totalPages; page++)
        {
            var result = await k7ServerService.QueryMediasAsync(BuildQuery(page, PageSize), cancellationToken);
            if (result?.Items is null)
                break;

            tracks.AddRange(result.Items.OfType<LiteMusicTrackDto>().Where(t => t.IndexedFileId.HasValue));
        }

        return tracks;
    }

    private async Task OnMediaTypeFilterChanged(MediaType value)
    {
        if (value == default || value == _selectedMediaType) return;

        _selectedMediaType = value;
        _filter = MediaBrowseFilterPresets.Empty;
        _intelligentSearch = null;
        _intelligentSearchResults = [];
        _totalCount = 0;
        _tableScopeKey = $"{value}:{Guid.NewGuid():N}";
        await LoadTagsAsync();
        await PersistFiltersAsync();
        await RefreshAllAsync();
    }

    private async Task OnSortChanged(MediaOrderingOption value)
    {
        if (value == _selectedSort) return;
        _selectedSort = value;

        // Sync sort key/direction from dropdown
        (_activeSortKey, _activeSortDirection) = MapOrderingToSortKey(value);

        await PersistFiltersAsync();
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

    private string? GetTableThumbUrl(LiteMediaDto item)
    {
        var picture = LiteMediaThumbnailHelper.ResolvePicture(item);
        return picture?.GetUri(MetadataPictureSize.Small)?.OriginalString;
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
        LiteSerieEpisodeDto => MediaCardVariant.Backdrop,
        _ => MediaCardVariant.Poster
    };

    private async Task ExcludeForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            await RefreshBrowseAsync();
    }

    private async Task ExcludeForOthers(MediaCardViewModel item)
    {
        await MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);
        await RefreshBrowseAsync();
    }

    private async Task RefreshBrowseAsync()
    {
        if (_dataTable is not null)
            await _dataTable.RefreshAsync();
        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

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
                var page = await k7ServerService.QueryMediasAsync(query);
                var offset = mid - ((mid / PageSize) * PageSize);
                var items = page?.Items?.ToList();

                if (items is null || offset >= items.Count) break;

                var itemTitle = items[offset].SortTitle ?? items[offset].Title ?? "";
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
