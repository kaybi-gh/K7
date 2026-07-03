using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpacePlaylistsPage
{
    private const string FilterStorageKey = "my-space-playlists";
    private const int PageSize = 500;

    private List<LitePlaylistDto> _playlists = [];
    private List<SharedPlaylistBrowseDto> _sharedPlaylists = [];
    private bool _loading = true;
    private bool _showShared;
    private bool _canCreate;
    private MediaType? _mediaTypeFilter;
    private LibraryItemOrderingOption _selectedSort = LibraryItemOrderingOption.LastListenedDesc;
    private List<ButtonGroupOption<MediaType?>> _mediaTypeOptions = [];
    private bool _musicIntelligenceAvailable;
    private BrowseView<LitePlaylistDto>? _browseView;
    private K7DataTable<LitePlaylistDto>? _dataTable;
    private string? _activeSortKey = "lastListened";
    private K7SortDirection _activeSortDirection = K7SortDirection.Descending;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private ISocialUserService SocialUserService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _mediaTypeOptions =
        [
            new(null, Label: L["All"]),
            new(MediaType.MusicTrack, Label: L["Music"]),
            new(MediaType.Movie, Label: L["FilterMovies"]),
            new(MediaType.SerieEpisode, Label: L["TVShows"])
        ];

        _canCreate = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        await LoadPersistedFiltersAsync();
        (_activeSortKey, _activeSortDirection) = MySpaceLibraryBrowseSort.MapPlaylistOrderingToSortKey(_selectedSort);
        await LoadPlaylistsAsync();

        try
        {
            var status = await ServerPreferences.GetMusicIntelligenceStatusAsync();
            _musicIntelligenceAvailable = status?.IsAvailable ?? false;
        }
        catch
        {
            _musicIntelligenceAvailable = false;
        }
    }

    private async Task LoadPlaylistsAsync()
    {
        _loading = true;
        if (_showShared)
        {
            _sharedPlaylists = (await SocialUserService.GetSharedPlaylistsAsync()).ToList();
        }
        else
        {
            var result = await K7ServerService.GetPlaylistsAsync(
                pageSize: PageSize,
                mediaType: _mediaTypeFilter,
                orderBy: _selectedSort);
            _playlists = result?.Items?.ToList() ?? [];
        }

        _loading = false;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private Task<K7DataTableResult<LitePlaylistDto>> LoadTableDataAsync(
        K7DataTableState<LitePlaylistDto> state, CancellationToken cancellationToken)
    {
        if (state.Count <= 0)
            return Task.FromResult(new K7DataTableResult<LitePlaylistDto>([], 0));

        var items = _playlists
            .Skip(state.StartIndex)
            .Take(state.Count)
            .ToList();

        return Task.FromResult(new K7DataTableResult<LitePlaylistDto>(items, _playlists.Count));
    }

    private async Task SetMediaTypeFilter(MediaType? mediaType)
    {
        _mediaTypeFilter = mediaType;
        await PersistFiltersAsync();
        await LoadPlaylistsAsync();
    }

    private async Task OnSortChanged(LibraryItemOrderingOption value)
    {
        if (value == _selectedSort)
            return;

        _selectedSort = value;
        (_activeSortKey, _activeSortDirection) = MySpaceLibraryBrowseSort.MapPlaylistOrderingToSortKey(value);
        await PersistFiltersAsync();
        await LoadPlaylistsAsync();
    }

    private async Task OnTableSortChanged(SortChangedEventArgs args)
    {
        _activeSortKey = args.SortKey;
        _activeSortDirection = args.Direction;

        var ordering = MySpaceLibraryBrowseSort.MapSortKeyToPlaylistOrdering(args.SortKey, args.Direction);
        if (ordering is not null && ordering != _selectedSort)
        {
            _selectedSort = ordering.Value;
            await PersistFiltersAsync();
            await LoadPlaylistsAsync();
            return;
        }

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private async Task OnShowSharedChanged(bool value)
    {
        _showShared = value;
        await LoadPlaylistsAsync();
    }

    private void NavigateToPlaylist(LitePlaylistDto playlist) =>
        NavigationManager.NavigateTo(GetPlaylistHref(playlist));

    private void OnColumnPickerRequested() =>
        _dataTable?.ToggleColumnPicker();

    private async Task LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<MySpacePlaylistsFilterState>(FilterStorageKey);
            if (state is null)
                return;

            if (state.MediaType is int mediaTypeValue && Enum.IsDefined(typeof(MediaType), mediaTypeValue))
                _mediaTypeFilter = (MediaType)mediaTypeValue;

            if (Enum.IsDefined(typeof(LibraryItemOrderingOption), state.Sort))
                _selectedSort = (LibraryItemOrderingOption)state.Sort;
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
            await PageFilterStorage.SaveAsync(
                FilterStorageKey,
                new MySpacePlaylistsFilterState((int?)_mediaTypeFilter, (int)_selectedSort));
        }
        catch
        {
            // Non-critical
        }
    }

    private string GetSortLabel(LibraryItemOrderingOption option) =>
        MySpaceLibraryBrowseSort.GetLabel(option, LibrarySortL);

    private async Task OpenCreatePlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreatePlaylistDialog>("Nouvelle playlist", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadPlaylistsAsync();
    }

    private async Task OpenCreateSmartPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SmartPlaylistDialog>("Nouvelle smart playlist", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid id })
        {
            try { await K7ServerService.EvaluateSmartPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/smart-playlists/{id}");
        }
    }

    private async Task OpenAiPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<AiSmartPlaylistDialog>(L["AiPlaylist"], null, options);
        await dialog.Result;
    }

    private string GetPlaylistHref(LitePlaylistDto playlist) =>
        playlist.IsSmartPlaylist
            ? $"/smart-playlists/{playlist.Id}"
            : $"/playlists/{playlist.Id}";

    private string GetPlaylistSubtitle(LitePlaylistDto playlist)
    {
        var parts = new List<string> { $"{playlist.ItemCount} {GetItemLabel(playlist.MediaType)}" };
        if (playlist.LastListenedAt is { } lastListened)
            parts.Add(FormatLastListened(lastListened));
        return string.Join(" · ", parts);
    }

    private string GetPlaylistItemCountLabel(LitePlaylistDto playlist) =>
        $"{playlist.ItemCount} {GetItemLabel(playlist.MediaType)}";

    private string FormatLastListenedOrDash(DateTimeOffset? lastListenedAt) =>
        lastListenedAt is { } lastListened ? FormatLastListened(lastListened) : "-";

    private string GetItemLabel(MediaType mediaType) => mediaType switch
    {
        MediaType.MusicTrack => L["Tracks"],
        MediaType.Movie => L["Movies"],
        MediaType.SerieEpisode => L["Episodes"],
        _ => L["Items"]
    };

    private string FormatLastListened(DateTimeOffset dateTime)
    {
        var diff = DateTimeOffset.UtcNow - dateTime.ToUniversalTime();
        if (diff.TotalMinutes < 1)
            return L["LastListenedJustNow"];
        if (diff.TotalMinutes < 60)
            return L["LastListenedMinutes", (int)diff.TotalMinutes];
        if (diff.TotalHours < 24)
            return L["LastListenedHours", (int)diff.TotalHours];
        return L["LastListenedDays", (int)diff.TotalDays];
    }

    private sealed record MySpacePlaylistsFilterState(int? MediaType, int Sort);
}
