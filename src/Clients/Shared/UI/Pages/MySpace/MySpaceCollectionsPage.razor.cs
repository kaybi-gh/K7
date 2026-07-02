using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceCollectionsPage
{
    private const string FilterStorageKey = "my-space-collections";
    private const int PageSize = 500;

    private List<LiteCollectionDto> _collections = [];
    private bool _loading = true;
    private bool _canCreate;
    private LibraryItemOrderingOption _selectedSort = LibraryItemOrderingOption.LastModifiedDesc;
    private BrowseView<LiteCollectionDto>? _browseView;
    private K7DataTable<LiteCollectionDto>? _dataTable;
    private string? _activeSortKey = "lastModified";
    private K7SortDirection _activeSortDirection = K7SortDirection.Descending;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _canCreate = await FeatureAccess.HasCapabilityAsync(Capability.CanCreatePlaylist);
        await LoadPersistedFiltersAsync();
        (_activeSortKey, _activeSortDirection) = MySpaceLibraryBrowseSort.MapCollectionOrderingToSortKey(_selectedSort);
        await LoadCollectionsAsync();
    }

    private async Task LoadCollectionsAsync()
    {
        _loading = true;
        var result = await K7ServerService.GetCollectionsAsync(pageSize: PageSize, orderBy: _selectedSort);
        _collections = result?.Items?.ToList() ?? [];
        _loading = false;

        if (_dataTable is not null)
            await _dataTable.RefreshAsync();

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private Task<K7DataTableResult<LiteCollectionDto>> LoadTableDataAsync(
        K7DataTableState<LiteCollectionDto> state, CancellationToken cancellationToken)
    {
        if (state.Count <= 0)
            return Task.FromResult(new K7DataTableResult<LiteCollectionDto>([], 0));

        var items = _collections
            .Skip(state.StartIndex)
            .Take(state.Count)
            .ToList();

        return Task.FromResult(new K7DataTableResult<LiteCollectionDto>(items, _collections.Count));
    }

    private async Task OnSortChanged(LibraryItemOrderingOption value)
    {
        if (value == _selectedSort)
            return;

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

    private void NavigateToCollection(LiteCollectionDto collection) =>
        NavigationManager.NavigateTo(GetCollectionHref(collection));

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
                new MySpaceCollectionsFilterState((int)_selectedSort));
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

    private string GetCollectionHref(LiteCollectionDto collection) =>
        $"/collections/{collection.Id}";

    private string GetCollectionSubtitle(LiteCollectionDto collection)
    {
        var parts = new List<string> { $"{collection.ItemCount} {L["Items"]}" };
        if (collection.IsPublic)
            parts.Add(L["Public"]);
        return string.Join(" · ", parts);
    }

    private sealed record MySpaceCollectionsFilterState(int Sort);
}
