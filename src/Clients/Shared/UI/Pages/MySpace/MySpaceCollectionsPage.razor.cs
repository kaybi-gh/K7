using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceCollectionsPage : IAsyncDisposable
{
    private const string FilterStorageKey = "my-space-collections";
    private const int PageSize = 500;

    private List<MySpaceCollectionBrowseItem> _items = [];
    private bool _loading = true;
    private bool _showShared;
    private bool _canCreate;
    private bool _selectionMode;
    private bool _deleting;
    private readonly HashSet<Guid> _selectedIds = [];
    private LibraryItemOrderingOption _selectedSort = LibraryItemOrderingOption.LastModifiedDesc;
    private BrowseView<MySpaceCollectionBrowseItem>? _browseView;
    private K7DataTable<MySpaceCollectionBrowseItem>? _dataTable;
    private string? _activeSortKey = "lastModified";
    private K7SortDirection _activeSortDirection = K7SortDirection.Descending;
    private SelectionModeKeyboardBinder? _selectionKeys;

    private int OwnedCount => _items.Count(item => item.IsOwned);
    private int SelectedCount => _selectedIds.Count;
    private bool AllSelected => OwnedCount > 0 && _selectedIds.Count == OwnedCount;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private ISocialUserService SocialUserService { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _selectionKeys = new SelectionModeKeyboardBinder(
            SpatialNav,
            onEscape: () => _ = InvokeAsync(OnSelectionEscape),
            onSelectAll: () => _ = InvokeAsync(OnSelectionSelectAll));

        _canCreate = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        await LoadPersistedFiltersAsync();
        (_activeSortKey, _activeSortDirection) = MySpaceLibraryBrowseSort.MapCollectionOrderingToSortKey(_selectedSort);
        await LoadCollectionsAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_selectionKeys is not null)
            await _selectionKeys.DisposeAsync();
    }

    private async Task LoadCollectionsAsync()
    {
        _loading = true;

        var result = await K7ServerService.GetCollectionsAsync(pageSize: PageSize, orderBy: _selectedSort);
        var current = result?.Items?.ToList() ?? [];

        IReadOnlyList<SharedCollectionBrowseDto>? shared = null;
        if (_showShared)
            shared = await SocialUserService.GetSharedCollectionsAsync();

        _items = MySpaceSharedBrowseHelper.BuildCollectionItems(current, shared, _selectedSort).ToList();
        _loading = false;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private Task<K7DataTableResult<MySpaceCollectionBrowseItem>> LoadTableDataAsync(
        K7DataTableState<MySpaceCollectionBrowseItem> state, CancellationToken cancellationToken)
    {
        if (state.Count <= 0)
            return Task.FromResult(new K7DataTableResult<MySpaceCollectionBrowseItem>([], 0));

        var items = _items
            .Skip(state.StartIndex)
            .Take(state.Count)
            .ToList();

        return Task.FromResult(new K7DataTableResult<MySpaceCollectionBrowseItem>(items, _items.Count));
    }

    private async Task OnSortChanged(LibraryItemOrderingOption value)
    {
        if (value == _selectedSort)
            return;

        ExitSelectionMode();
        _selectedSort = value;
        (_activeSortKey, _activeSortDirection) = MySpaceLibraryBrowseSort.MapCollectionOrderingToSortKey(value);
        await PersistFiltersAsync();
        await LoadCollectionsAsync();
    }

    private async Task OnTableSortChanged(SortChangedEventArgs args)
    {
        _activeSortKey = args.SortKey;
        _activeSortDirection = args.Direction;

        var ordering = MySpaceLibraryBrowseSort.MapSortKeyToCollectionOrdering(args.SortKey, args.Direction);
        if (ordering is not null && ordering != _selectedSort)
        {
            _selectedSort = ordering.Value;
            await PersistFiltersAsync();
            await LoadCollectionsAsync();
            return;
        }

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private async Task OnShowSharedChanged(bool value)
    {
        ExitSelectionMode();
        _showShared = value;
        await PersistFiltersAsync();
        await LoadCollectionsAsync();
    }

    private void EnterSelectionMode()
    {
        _selectionMode = true;
        _selectedIds.Clear();
        _dataTable?.InvalidateLayout();
        _ = _selectionKeys?.SetEnabledAsync(true);
    }

    private void ExitSelectionMode()
    {
        _selectionMode = false;
        _selectedIds.Clear();
        _dataTable?.InvalidateLayout();
        _ = _selectionKeys?.SetEnabledAsync(false);
    }

    private void ToggleSelection(Guid id)
    {
        if (!_selectedIds.Remove(id))
            _selectedIds.Add(id);

        _dataTable?.Rerender();
    }

    private void ToggleSelectAll()
    {
        if (AllSelected)
            _selectedIds.Clear();
        else
            SelectAll();

        _dataTable?.Rerender();
    }

    private void SelectAll()
    {
        _selectedIds.Clear();
        foreach (var item in _items)
        {
            if (item.IsOwned)
                _selectedIds.Add(item.Id);
        }
    }

    private void OnSelectionEscape()
    {
        if (_deleting)
            return;

        ExitSelectionMode();
    }

    private void OnSelectionSelectAll()
    {
        if (!_selectionMode || _deleting)
            return;

        SelectAll();
        _dataTable?.Rerender();
    }

    private bool IsSelected(Guid id) => _selectedIds.Contains(id);

    private void OnSelectKeyDown(KeyboardEventArgs e, Guid id)
    {
        if (e.Key is not ("Enter" or " "))
            return;

        ToggleSelection(id);
    }

    private void OnCollectionActivated(MySpaceCollectionBrowseItem item)
    {
        if (_selectionMode && item.IsOwned)
            ToggleSelection(item.Id);
        else
            NavigateToCollection(item);
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
                var item = _items.FirstOrDefault(c => c.Id == id && c.IsOwned);
                if (item is null)
                    continue;

                try
                {
                    await K7ServerService.DeleteCollectionAsync(id);
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
        await LoadCollectionsAsync();

        if (failed == 0)
            Snackbar.Add(string.Format(L["DeleteSelectedSuccess"], count), K7Severity.Success);
        else if (failed == count)
            Snackbar.Add(L["DeleteSelectedError"], K7Severity.Error);
        else
            Snackbar.Add(string.Format(L["DeleteSelectedPartial"], count - failed, failed), K7Severity.Warning);
    }

    private void NavigateToCollection(MySpaceCollectionBrowseItem item) =>
        NavigationManager.NavigateTo(GetCollectionHref(item));

    private void OnColumnPickerRequested() =>
        _dataTable?.ToggleColumnPicker();

    private async Task LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<MySpaceCollectionsFilterState>(FilterStorageKey);
            if (state is null)
                return;

            if (Enum.IsDefined(typeof(LibraryItemOrderingOption), state.Sort))
                _selectedSort = (LibraryItemOrderingOption)state.Sort;

            _showShared = state.ShowShared;
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
                new MySpaceCollectionsFilterState((int)_selectedSort, _showShared));
        }
        catch
        {
            // Non-critical
        }
    }

    private string GetSortLabel(LibraryItemOrderingOption option) =>
        MySpaceLibraryBrowseSort.GetLabel(option, LibrarySortL);

    private async Task OpenCreateCollectionDialog()
    {
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<CreateCollectionDialog>("Nouvelle collection", null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
            await LoadCollectionsAsync();
    }

    private string GetCollectionItemCountLabel(LiteCollectionDto collection) =>
        $"{collection.ItemCount} {L["Items"]}";

    private string GetCollectionHref(MySpaceCollectionBrowseItem item)
    {
        if (item.Owner is { IsFederated: true } owner)
            return SocialUserNavigation.GetProfileHref(owner);

        return $"/collections/{item.Id}";
    }

    private string GetCollectionSubtitle(MySpaceCollectionBrowseItem item)
    {
        var collection = item.Collection;
        var parts = new List<string> { $"{collection.ItemCount} {L["Items"]}" };
        if (collection.IsPublic)
            parts.Add(L["Public"]);
        if (item.Owner is { } owner)
            parts.Add(MySpaceSharedBrowseHelper.FormatOwner(owner));
        return string.Join(" · ", parts);
    }

    private string GetOwnerLabel(MySpaceCollectionBrowseItem item) =>
        item.Owner is { } owner ? MySpaceSharedBrowseHelper.FormatOwner(owner) : "-";

    private sealed record MySpaceCollectionsFilterState(int Sort, bool ShowShared = false);
}
