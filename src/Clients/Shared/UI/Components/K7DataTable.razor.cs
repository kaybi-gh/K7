using System.Text.Json;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.UI.Components;

[CascadingTypeParameter(nameof(TItem))]
public partial class K7DataTable<TItem> : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private ILogger<K7DataTable<TItem>> Logger { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public Func<K7DataTableState<TItem>, CancellationToken, Task<K7DataTableResult<TItem>>>? ServerData { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? ActiveSortKey { get; set; }
    [Parameter] public K7SortDirection ActiveSortDirection { get; set; } = K7SortDirection.Ascending;
    [Parameter] public EventCallback<SortChangedEventArgs> OnSortChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }
    [Parameter] public float RowHeight { get; set; } = 48;
    [Parameter] public int OverscanCount { get; set; } = 10;
    [Parameter] public bool ShowToolbar { get; set; } = true;
    [Parameter] public bool Surface { get; set; } = true;
    [Parameter] public bool Striped { get; set; } = true;
    [Parameter] public string? Height { get; set; }

    /// <summary>
    /// When set, the columns hidden through the column picker are stored on the device
    /// (IDeviceStorageService) under this key and hidden again on the next visit.
    /// </summary>
    [Parameter] public string? PersistenceKey { get; set; }

    /// <summary>
    /// Minimum width in pixels kept for each column without a fixed width. When set, the table
    /// gets a minimum width (fixed columns + this value per flexible column) and scrolls
    /// horizontally on narrow screens instead of drawing the columns on top of each other.
    /// </summary>
    [Parameter] public int? FlexColumnMinWidth { get; set; }
    [Parameter] public Func<TItem, string?>? RowId { get; set; }
    [Parameter] public Func<TItem, string?>? RowClass { get; set; }

    private readonly record struct IndexedRow(TItem Item, int Index);

    private readonly List<K7DataColumn<TItem>> _columns = [];
    private IReadOnlyList<K7DataColumn<TItem>> _visibleColumns = [];
    private IReadOnlyList<K7DataColumn<TItem>> _hideableColumns = [];
    private Virtualize<IndexedRow>? _virtualizeRef;
    private List<IndexedRow>? _indexedItems;
    private bool _columnPickerOpen;
    private bool _columnPickerLayerPushed;
    private ElementReference _columnPickerDropdown;
    private ElementReference _scrollRef;
    private DotNetObjectReference<LayerCloseCallback>? _columnPickerCloseRef;
    private IJSObjectReference? _browseViewModule;
    private bool _keyNavInitialized;
    private HashSet<string>? _storedHiddenColumns;
    private string? _storedHiddenColumnsKey;
    private bool _disposed;
    private bool _needsRender = true;
    private bool _pendingVirtualizeRefresh;
    private string? _prevSortKey;
    private K7SortDirection _prevSortDirection;
    private IList<TItem>? _prevItems;
    private readonly Dictionary<string, ItemsProviderResult<IndexedRow>> _serverDataCache = new();

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_prevItems, Items))
        {
            _prevItems = Items;
            _indexedItems = Items?.Select((item, index) => new IndexedRow(item, index)).ToList();
            _needsRender = true;
            _pendingVirtualizeRefresh = true;
        }
    }

    protected override bool ShouldRender()
    {
        if (_prevSortKey != ActiveSortKey || _prevSortDirection != ActiveSortDirection)
        {
            _prevSortKey = ActiveSortKey;
            _prevSortDirection = ActiveSortDirection;
            InvalidateServerDataCache();
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
            CloseColumnPickerAsync().FireAndForget(Logger);
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
        if (firstRender && ServerData is not null)
            _pendingVirtualizeRefresh = true;

        if (_pendingVirtualizeRefresh && _virtualizeRef is not null)
        {
            _pendingVirtualizeRefresh = false;
            await _virtualizeRef.RefreshDataAsync();
        }

        if (!_disposed && !_keyNavInitialized && HasContent())
        {
            _browseViewModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");
            await _browseViewModule.InvokeVoidAsync("initTableKeyNav", _scrollRef, RowHeight);
            _keyNavInitialized = true;
        }

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
        InvokeAsync(() =>
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
        }).FireAndForget(Logger);
    }

    private Task OnColumnPickerBackdropClick() => CloseColumnPickerAsync();

    private Task OnColumnPickerCloseClick() => CloseColumnPickerAsync();

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_columnPickerOpen && !ShowToolbar)
        {
            await CloseColumnPickerLayerAsync();
        }

        _columnPickerCloseRef?.Dispose();

        if (_browseViewModule is not null)
        {
            try
            {
                if (_keyNavInitialized)
                    await _browseViewModule.InvokeVoidAsync("disposeTableKeyNav", _scrollRef);

                await _browseViewModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _browseViewModule = null;
    }

    private bool HasContent() =>
        Items is { Count: > 0 } || ServerData is not null;

    internal void AddColumn(K7DataColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            // Applied as the column registers, before the first render: no flash of a
            // column the user had hidden.
            if (column.Hideable && column.IsVisible && StoredHiddenColumns.Contains(column.ColumnKey))
                column.SetVisible(false);

            _columns.Add(column);
            UpdateColumnCaches();
            _needsRender = true;
            StateHasChanged();
        }
    }

    internal void RemoveColumn(K7DataColumn<TItem> column)
    {
        _columns.Remove(column);
        UpdateColumnCaches();
    }

    public async Task RefreshAsync()
    {
        InvalidateServerDataCache();

        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
            // RefreshDataAsync reloads the rows but does not render them: without this the
            // table keeps showing the old rows (removed item still listed) until a scroll.
            _needsRender = true;
            StateHasChanged();
            return;
        }

        if (ServerData is not null)
        {
            _pendingVirtualizeRefresh = true;
            _needsRender = true;
            StateHasChanged();
        }
    }

    public void InvalidateLayout()
    {
        UpdateColumnCaches();
        Rerender();
    }

    public void Rerender()
    {
        _needsRender = true;
        StateHasChanged();
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

        var cacheKey = BuildServerDataCacheKey(state);
        if (_serverDataCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var result = await ServerData(state, request.CancellationToken);
            var rows = result.Items
                .Select((item, offset) => new IndexedRow(item, request.StartIndex + offset))
                .ToList();
            var providerResult = new ItemsProviderResult<IndexedRow>(rows, result.TotalItemCount);
            _serverDataCache[cacheKey] = providerResult;
            return providerResult;
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            return default;
        }
    }

    private string GetRowClass(IndexedRow row) =>
        $"{RowClass?.Invoke(row.Item)} {(Striped && row.Index % 2 == 1 ? "k7-data-table-row--alt" : "")}".Trim();

    // Virtualize is position-based; business RowId keys collide when the same item
    // appears twice (overlapping page fetches / live list shifts during fast scroll).
    private object GetRowKey(IndexedRow row) => row.Index;

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
        UpdateColumnCaches();
        _needsRender = true;
        StateHasChanged();
        SaveHiddenColumns();
    }

    private HashSet<string> StoredHiddenColumns
    {
        get
        {
            // The key can change while the component is reused (another library group).
            if (_storedHiddenColumns is not null && _storedHiddenColumnsKey == PersistenceKey)
                return _storedHiddenColumns;

            _storedHiddenColumnsKey = PersistenceKey;
            _storedHiddenColumns = PersistenceKey is { } key && ReadHiddenColumnsMap().TryGetValue(key, out var hidden)
                ? [.. hidden]
                : [];
            return _storedHiddenColumns;
        }
    }

    internal bool IsStoredHidden(string columnKey) => StoredHiddenColumns.Contains(columnKey);

    private Dictionary<string, string[]> ReadHiddenColumnsMap()
    {
        try
        {
            var json = DeviceStorage.Get(PreferenceKeys.DATA_TABLE_HIDDEN_COLUMNS);
            return string.IsNullOrEmpty(json)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, string[]>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void SaveHiddenColumns()
    {
        if (PersistenceKey is not { } key)
            return;

        // Columns switched off by their Visible parameter keep whatever was stored for them.
        var hidden = _hideableColumns
            .Where(column => column.VisibleParameter
                ? !column.IsVisible
                : StoredHiddenColumns.Contains(column.ColumnKey))
            .Select(column => column.ColumnKey)
            .Distinct()
            .ToArray();
        _storedHiddenColumns = [.. hidden];

        var map = ReadHiddenColumnsMap();
        if (hidden.Length == 0)
            map.Remove(key);
        else
            map[key] = hidden;

        DeviceStorage.Set(PreferenceKeys.DATA_TABLE_HIDDEN_COLUMNS, JsonSerializer.Serialize(map));
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

    private void UpdateColumnCaches()
    {
        _visibleColumns = _columns.Where(column => column.IsVisible).ToList();
        _hideableColumns = _columns.Where(column => column.Hideable).ToList();
    }

    private string ScrollContainerStyle
    {
        get
        {
            var parts = new List<string>
            {
                FormattableString.Invariant($"--k7-data-table-row-height: {RowHeight}px")
            };

            if (Height is not null)
            {
                parts.Add(FormattableString.Invariant($"height: {Height}"));
                parts.Add("flex: none");
            }

            return string.Join("; ", parts);
        }
    }

    private string? TableStyle
    {
        get
        {
            if (FlexColumnMinWidth is not { } flexMinWidth)
                return null;

            var minWidth = 0d;
            foreach (var column in _visibleColumns)
            {
                minWidth += column.Width is { } width
                    && width.EndsWith("px", StringComparison.Ordinal)
                    && double.TryParse(width.AsSpan(0, width.Length - 2), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pixels)
                    ? pixels
                    : flexMinWidth;
            }

            return FormattableString.Invariant($"min-width: {minWidth}px");
        }
    }

    private string RowStyle => FormattableString.Invariant($"height: {RowHeight}px; max-height: {RowHeight}px");

    private static string BuildServerDataCacheKey(K7DataTableState<TItem> state) =>
        FormattableString.Invariant($"{state.SortKey}:{state.SortDirection}:{state.StartIndex}:{state.Count}");

    private void InvalidateServerDataCache() => _serverDataCache.Clear();
}
