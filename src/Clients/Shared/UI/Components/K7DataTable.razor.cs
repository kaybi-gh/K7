using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
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
        }
    }

    private bool HasContent() =>
        Items is { Count: > 0 } || ServerData is not null;

    internal void AddColumn(K7DataColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
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
            _needsRender = true;
            await _virtualizeRef.RefreshDataAsync();
            StateHasChanged();
            return;
        }

        _pendingVirtualizeRefresh = true;
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

    private string RowStyle => FormattableString.Invariant($"height: {RowHeight}px; max-height: {RowHeight}px");

    private static string BuildServerDataCacheKey(K7DataTableState<TItem> state) =>
        FormattableString.Invariant($"{state.SortKey}:{state.SortDirection}:{state.StartIndex}:{state.Count}");

    private void InvalidateServerDataCache() => _serverDataCache.Clear();
}
