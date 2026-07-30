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
using Microsoft.AspNetCore.Components.Web;

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
    private bool _selectionMode;
    private bool _deleting;
    private readonly HashSet<Guid> _selectedIds = [];
    private BrowseView<LitePlaylistDto>? _browseView;
    private K7DataTable<LitePlaylistDto>? _dataTable;
    private string? _activeSortKey = "lastListened";
    private K7SortDirection _activeSortDirection = K7SortDirection.Descending;

    private int SelectedCount => _selectedIds.Count;
    private bool AllSelected => _playlists.Count > 0 && _selectedIds.Count == _playlists.Count;

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
        ExitSelectionMode();
        _mediaTypeFilter = mediaType;
        await PersistFiltersAsync();
        await LoadPlaylistsAsync();
    }

    private async Task OnSortChanged(LibraryItemOrderingOption value)
    {
        if (value == _selectedSort)
            return;

        ExitSelectionMode();
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
        ExitSelectionMode();
        _showShared = value;
        await LoadPlaylistsAsync();
    }

    private void EnterSelectionMode()
    {
        _selectionMode = true;
        _selectedIds.Clear();
    }

    private void ExitSelectionMode()
    {
        _selectionMode = false;
        _selectedIds.Clear();
    }

    private void ToggleSelection(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);
    }

    private void ToggleSelectAll()
    {
        if (AllSelected)
        {
            _selectedIds.Clear();
            return;
        }

        _selectedIds.Clear();
        foreach (var playlist in _playlists)
            _selectedIds.Add(playlist.Id);
    }

    private bool IsSelected(Guid id) => _selectedIds.Contains(id);

    private void OnSelectKeyDown(KeyboardEventArgs e, Guid id)
    {
        if (e.Key is not ("Enter" or " "))
            return;

        ToggleSelection(id);
    }

    private void OnPlaylistActivated(LitePlaylistDto playlist)
    {
        if (_selectionMode)
            ToggleSelection(playlist.Id);
        else
            NavigateToPlaylist(playlist);
    }

    private async Task ConfirmDeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0 || _deleting)
            return;

        var count = _selectedIds.Count;
        var result = await DialogService.ShowMessageBoxAsync(
            L["DeleteSelectedTitle"],
            string.Format(L["DeleteSelectedMessage"], count),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (result != true)
            return;

        _deleting = true;
        var failed = 0;

        try
        {
            foreach (var id in _selectedIds.ToList())
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == id);
                if (playlist is null)
                    continue;

                try
                {
                    if (playlist.IsDynamicPlaylist)
                        await K7ServerService.DeleteDynamicPlaylistAsync(id);
                    else
                        await K7ServerService.DeletePlaylistAsync(id);
                }
                catch
                {
                    failed++;
                }
            }
        }
        finally
        {
            _deleting = false;
        }

        ExitSelectionMode();
        await LoadPlaylistsAsync();

        if (failed == 0)
            Snackbar.Add(string.Format(L["DeleteSelectedSuccess"], count), K7Severity.Success);
        else if (failed == count)
            Snackbar.Add(L["DeleteSelectedError"], K7Severity.Error);
        else
            Snackbar.Add(string.Format(L["DeleteSelectedPartial"], count - failed, failed), K7Severity.Warning);
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

    private async Task OpenCreateDynamicPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<DynamicPlaylistDialog>(L["DynamicPlaylist"], null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid id })
        {
            try { await K7ServerService.EvaluateDynamicPlaylistAsync(id); } catch { }
            NavigationManager.NavigateTo($"/dynamic-playlists/{id}");
        }
    }

    private async Task OpenSmartPlaylistDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SmartPlaylistDialog>(L["SmartPlaylist"], null, options);
        await dialog.Result;
    }

    private string GetPlaylistHref(LitePlaylistDto playlist) =>
        playlist.IsDynamicPlaylist
            ? $"/dynamic-playlists/{playlist.Id}"
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
