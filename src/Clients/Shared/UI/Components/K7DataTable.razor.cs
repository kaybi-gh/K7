using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

[CascadingTypeParameter(nameof(TItem))]
public partial class K7DataTable<TItem> : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public Func<K7DataTableState<TItem>, CancellationToken, Task<K7DataTableResult<TItem>>>? ServerData { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? ActiveSortKey { get; set; }
    [Parameter] public K7SortDirection ActiveSortDirection { get; set; } = K7SortDirection.Ascending;
    [Parameter] public EventCallback<SortChangedEventArgs> OnSortChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }
    [Parameter] public string? PersistenceKey { get; set; }
    [Parameter] public float RowHeight { get; set; } = 48;
    [Parameter] public int OverscanCount { get; set; } = 5;
    [Parameter] public bool ShowToolbar { get; set; } = true;
    [Parameter] public bool Surface { get; set; } = true;
    [Parameter] public bool Striped { get; set; } = true;
    [Parameter] public string? Height { get; set; }
    [Parameter] public Func<TItem, string?>? RowId { get; set; }
    [Parameter] public Func<TItem, string?>? RowClass { get; set; }

    private readonly record struct IndexedRow(TItem Item, int Index);

    private readonly List<K7DataColumn<TItem>> _columns = [];
    private Virtualize<IndexedRow>? _virtualizeRef;
    private List<IndexedRow>? _indexedItems;
    private bool _columnPickerOpen;
    private bool _columnPickerLayerPushed;
    private ElementReference _columnPickerDropdown;
    private DotNetObjectReference<LayerCloseCallback>? _columnPickerCloseRef;
    private bool _needsRender = true;
    private string? _prevSortKey;
    private K7SortDirection _prevSortDirection;

    protected override void OnParametersSet()
    {
        _indexedItems = Items?.Select((item, index) => new IndexedRow(item, index)).ToList();
    }

    protected override bool ShouldRender()
    {
        if (_prevSortKey != ActiveSortKey || _prevSortDirection != ActiveSortDirection)
        {
            _prevSortKey = ActiveSortKey;
            _prevSortDirection = ActiveSortDirection;
            _needsRender = true;
        }

        if (!_needsRender)
        {
            return false;
        }
        _needsRender = false;
        return true;
    }

    public void ToggleColumnPicker()
    {
        if (_columnPickerOpen)
            _ = CloseColumnPickerAsync();
        else
            OpenColumnPicker();
    }

    private void OpenColumnPicker()
    {
        _columnPickerOpen = true;
        _needsRender = true;
        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_columnPickerOpen || ShowToolbar || _columnPickerLayerPushed)
        {
            return;
        }

        try
        {
            _columnPickerLayerPushed = true;
            _columnPickerCloseRef?.Dispose();
            _columnPickerCloseRef = DotNetObjectReference.Create(new LayerCloseCallback(OnColumnPickerLayerClosed));
            await SpatialNav.PushLayerAsync(_columnPickerDropdown, "popover", new SpatialNavLayerOptions
            {
                OnClose = _columnPickerCloseRef
            });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            _columnPickerLayerPushed = false;
        }
    }

    private async Task CloseColumnPickerAsync()
    {
        if (!_columnPickerOpen)
        {
            return;
        }

        _columnPickerOpen = false;

        if (!ShowToolbar && _columnPickerLayerPushed)
        {
            await CloseColumnPickerLayerAsync();
        }

        _needsRender = true;
        StateHasChanged();
    }

    private async Task CloseColumnPickerLayerAsync()
    {
        _columnPickerLayerPushed = false;
        _columnPickerCloseRef?.Dispose();
        _columnPickerCloseRef = null;

        try
        {
            await SpatialNav.PopLayerAsync(_columnPickerDropdown);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    private void OnColumnPickerLayerClosed()
    {
        _ = InvokeAsync(() =>
        {
            if (!_columnPickerOpen)
            {
                return Task.CompletedTask;
            }

            _columnPickerOpen = false;
            _columnPickerLayerPushed = false;
            _columnPickerCloseRef?.Dispose();
            _columnPickerCloseRef = null;
            _needsRender = true;
            StateHasChanged();
            return Task.CompletedTask;
        });
    }

    private Task OnColumnPickerBackdropClick() => CloseColumnPickerAsync();

    private Task OnColumnPickerCloseClick() => CloseColumnPickerAsync();

    public async ValueTask DisposeAsync()
    {
        if (_columnPickerOpen && !ShowToolbar)
        {
            await CloseColumnPickerLayerAsync();
        }

        _columnPickerCloseRef?.Dispose();
    }

    internal void AddColumn(K7DataColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            _needsRender = true;
            StateHasChanged();
        }
    }

    internal void RemoveColumn(K7DataColumn<TItem> column)
    {
        _columns.Remove(column);
    }

    public async Task RefreshAsync()
    {
        if (_virtualizeRef is not null)
        {
            _needsRender = true;
            await _virtualizeRef.RefreshDataAsync();
        }
    }

    private async ValueTask<ItemsProviderResult<IndexedRow>> ProvideItemsAsync(
        ItemsProviderRequest request)
    {
        if (ServerData is null) return default;

        var state = new K7DataTableState<TItem>(
            request.StartIndex,
            request.Count,
            ActiveSortKey,
            ActiveSortDirection);

        try
        {
            var result = await ServerData(state, request.CancellationToken);
            var rows = result.Items
                .Select((item, offset) => new IndexedRow(item, request.StartIndex + offset))
                .ToList();
            return new ItemsProviderResult<IndexedRow>(rows, result.TotalItemCount);
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            return default;
        }
    }

    private string GetRowClass(IndexedRow row) =>
        $"{RowClass?.Invoke(row.Item)} {(Striped && row.Index % 2 == 1 ? "k7-data-table-row--alt" : "")}".Trim();

    private async Task OnHeaderClick(K7DataColumn<TItem> column)
    {
        if (column.SortKey is null) return;

        var direction = column.SortKey == ActiveSortKey
            ? ActiveSortDirection is K7SortDirection.Ascending
                ? K7SortDirection.Descending
                : K7SortDirection.Ascending
            : K7SortDirection.Ascending;

        await OnSortChanged.InvokeAsync(new SortChangedEventArgs(column.SortKey, direction));
        await RefreshAsync();
    }

    private async Task OnRowClicked(TItem item)
    {
        await OnRowClick.InvokeAsync(item);
    }

    private void ToggleColumnVisibility(K7DataColumn<TItem> column)
    {
        column.SetVisible(!column.IsVisible);
        _needsRender = true;
        StateHasChanged();
    }

    private string GetSortIcon(K7DataColumn<TItem> column)
    {
        if (column.SortKey is null || column.SortKey != ActiveSortKey)
        {
            return Phosphor.CaretUpDown;
        }

        return ActiveSortDirection is K7SortDirection.Ascending
            ? Phosphor.SortAscending
            : Phosphor.SortDescending;
    }

    private async Task OnRowKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, TItem item)
    {
        if (e.Key is "Enter" or " ")
        {
            await OnRowClicked(item);
        }
    }

    private IEnumerable<K7DataColumn<TItem>> VisibleColumns =>
        _columns.Where(c => c.IsVisible);

    private string? ScrollStyle => Height is not null ? $"height: {Height}; flex: none" : null;

    private string RowStyle => FormattableString.Invariant($"height: {RowHeight}px");
}
