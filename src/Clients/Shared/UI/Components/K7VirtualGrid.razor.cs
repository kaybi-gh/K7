using System.Globalization;
using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7VirtualGrid<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public ItemsProviderDelegate<TItem>? ItemsProvider { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public int ItemWidth { get; set; } = 160;
    [Parameter] public int Spacing { get; set; } = 6;
    [Parameter] public float AspectRatio { get; set; } = 1.5f;
    [Parameter] public MediaCardVariant PlaceholderVariant { get; set; } = MediaCardVariant.Poster;
    [Parameter] public int FooterHeight { get; set; } = 44;
    [Parameter] public int OverscanCount { get; set; } = 5;
    [Parameter] public bool SingleColumnOnMobile { get; set; }
    [Parameter] public int? MaxColumnCount { get; set; }
    [Parameter] public EventCallback OnNearEnd { get; set; }

    private ElementReference _gridRef;
    private Virtualize<List<TItem>>? _virtualizeRef;
    private readonly Dictionary<string, CachedProviderRows> _rowsCache = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<K7VirtualGrid<TItem>>? _dotnetRef;

    private int _containerWidth;
    private int _lastColumnCount;
    private float _estimatedRowHeight = 300;
    private int _lastTotalRows;
    private bool _observing;
    private bool _disposed;

    private List<List<TItem>> _rows = [];

    protected override void OnParametersSet()
    {
        if (Items is not null)
        {
            RebuildRows();
        }
        else if (_containerWidth > 0)
        {
            var cols = CalculateColumnCount();
            if (cols != _lastColumnCount)
            {
                _lastColumnCount = cols;
                _rowsCache.Clear();
            }
        }

        UpdateEstimatedRowHeight();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");
        }

        if (!_observing && HasContent() && _module is not null)
        {
            _observing = true;
            _dotnetRef ??= DotNetObjectReference.Create(this);

            var initialWidth = await _module.InvokeAsync<int>("observeContainerWidth", _gridRef, _dotnetRef);
            if (initialWidth > 0 && _containerWidth == 0)
            {
                _containerWidth = initialWidth;
                UpdateEstimatedRowHeight();
                // Rows were chunked with the width=0 fallback (4 cols). Rebuild once we know the real width
                // so CSS column count and item chunking stay in sync. ResizeObserver may report the same
                // width and early-return without rebuilding.
                if (Items is not null)
                    RebuildRows();
                else
                    _lastColumnCount = CalculateColumnCount();
                StateHasChanged();
            }

            await _module.InvokeVoidAsync("initGridKeyNav", _gridRef, _estimatedRowHeight);
            SyncKeyNavExtent();
            try
            {
                await _module.InvokeVoidAsync("setVirtualKeyNavItemHeight", _gridRef, _estimatedRowHeight);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or InvalidOperationException)
            {
            }
        }
    }

    [JSInvokable]
    public Task OnVirtualScrollNearEnd()
    {
        if (_disposed || !OnNearEnd.HasDelegate)
            return Task.CompletedTask;

        return InvokeAsync(OnNearEnd.InvokeAsync);
    }

    [JSInvokable]
    public async Task OnContainerWidthChanged(int width)
    {
        if (_disposed || width == _containerWidth) return;

        var isFirstMeasure = _containerWidth == 0;
        var previousRowHeight = _estimatedRowHeight;
        var wasCompact = IsCompactGrid;
        _containerWidth = width;
        UpdateEstimatedRowHeight();

        var newCols = CalculateColumnCount();
        var colsChanged = newCols != _lastColumnCount || isFirstMeasure;
        var rowHeightChanged = Math.Abs(_estimatedRowHeight - previousRowHeight) >= 1f;
        var compactChanged = wasCompact != IsCompactGrid;

        if (colsChanged)
        {
            if (Items is not null)
            {
                RebuildRows();
            }

            _lastColumnCount = newCols;
            _rowsCache.Clear();
        }

        if (_virtualizeRef is not null && (colsChanged || rowHeightChanged || compactChanged || isFirstMeasure))
        {
            await _virtualizeRef.RefreshDataAsync();
        }

        if (colsChanged || rowHeightChanged || compactChanged || isFirstMeasure)
        {
            if (_module is not null && rowHeightChanged)
            {
                try
                {
                    await _module.InvokeVoidAsync("setVirtualKeyNavItemHeight", _gridRef, _estimatedRowHeight);
                    SyncKeyNavExtent();
                }
                catch (Exception ex) when (ex is JSDisconnectedException or JSException or InvalidOperationException)
                {
                }
            }

            await InvokeAsync(StateHasChanged);
        }
    }

    public async Task RefreshAsync()
    {
        _rowsCache.Clear();

        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }

    public void PatchLoadedSlots(Func<int, TItem> itemAtIndex)
    {
        if (_rowsCache.Count == 0)
            return;

        var changed = false;
        foreach (var cached in _rowsCache.Values)
        {
            var rowOffset = 0;
            foreach (var row in cached.Rows)
            {
                for (var c = 0; c < row.Count; c++)
                {
                    var absIndex = (cached.RowStart + rowOffset) * cached.Cols + c;
                    var next = itemAtIndex(absIndex);
                    if (Equals(row[c], next))
                        continue;

                    row[c] = next;
                    changed = true;
                }

                rowOffset++;
            }
        }

        if (changed)
            StateHasChanged();
    }

    public void ScrollToItemIndex(int itemIndex)
    {
        var cols = CalculateColumnCount();
        var rowIndex = itemIndex / cols;
        var scrollTop = rowIndex * _estimatedRowHeight;

        if (_module is not null)
        {
            _ = _module.InvokeVoidAsync("scrollTo", _gridRef, scrollTop);
        }
    }

    private async ValueTask<ItemsProviderResult<List<TItem>>> ProvideRowsAsync(
        ItemsProviderRequest request)
    {
        if (ItemsProvider is null) return default;

        var cols = CalculateColumnCount();
        var cacheKey = FormattableString.Invariant($"{cols}:{request.StartIndex}:{request.Count}");
        if (_rowsCache.TryGetValue(cacheKey, out var cached))
        {
            // Virtualize can re-ask a range it already mounted (fast scroll then stop).
            // Re-run the provider so cancelled / queued pages for this window start again
            // without replacing the cached row lists PatchLoadedSlots mutates.
            _ = ItemsProvider(
                new ItemsProviderRequest(request.StartIndex * cols, request.Count * cols, CancellationToken.None));
            return new ItemsProviderResult<List<TItem>>(cached.Rows, cached.TotalRows);
        }

        try
        {
            var itemStart = request.StartIndex * cols;
            var itemCount = request.Count * cols;

            var result = await ItemsProvider(
                new ItemsProviderRequest(itemStart, itemCount, request.CancellationToken));

            if (result.Items is null)
            {
                request.CancellationToken.ThrowIfCancellationRequested();
                return new ItemsProviderResult<List<TItem>>([], _lastTotalRows);
            }

            var rows = result.Items
                .Chunk(cols)
                .Select((chunk, i) => (List<TItem>)new IndexedRow(request.StartIndex + i, chunk))
                .ToList();

            var totalRows = result.TotalItemCount > 0
                ? (int)Math.Ceiling((double)result.TotalItemCount / cols)
                : _lastTotalRows;
            _lastColumnCount = cols;
            _lastTotalRows = totalRows;

            _rowsCache[cacheKey] = new CachedProviderRows
            {
                RowStart = request.StartIndex,
                Cols = cols,
                Rows = rows,
                TotalRows = totalRows
            };
            SyncKeyNavExtent();
            return new ItemsProviderResult<List<TItem>>(rows, totalRows);
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    private void RebuildRows()
    {
        if (Items is null || Items.Count == 0)
        {
            _rows = [];
            _lastTotalRows = 0;
            return;
        }

        var cols = CalculateColumnCount();
        var chunks = Items.Chunk(cols).ToList();

        if (_lastColumnCount == cols
            && _rows.Count > 0
            && _rows[0] is IndexedRow
            && chunks.Count >= _rows.Count)
        {
            var lastOld = _rows[^1];
            var lastChunk = chunks[_rows.Count - 1];
            if (lastOld.Count != lastChunk.Length)
            {
                lastOld.Clear();
                lastOld.AddRange(lastChunk);
            }

            for (var i = _rows.Count; i < chunks.Count; i++)
                _rows.Add(new IndexedRow(i, chunks[i]));

            _lastColumnCount = cols;
            _lastTotalRows = _rows.Count;
            SyncKeyNavExtent();
            return;
        }

        _lastColumnCount = cols;
        _rows = chunks
            .Select((chunk, i) => (List<TItem>)new IndexedRow(i, chunk))
            .ToList();
        _lastTotalRows = _rows.Count;
        SyncKeyNavExtent();
    }

    private int CalculateColumnCount()
    {
        if (SingleColumnOnMobile
            && _containerWidth > 0
            && _containerWidth < VirtualGridLayout.CompactBreakpoint)
        {
            return 1;
        }

        return VirtualGridLayout.CalculateColumnCount(_containerWidth, ItemWidth, Spacing, AspectRatio, MaxColumnCount);
    }

    private int GetEffectiveSpacing() =>
        VirtualGridLayout.GetEffectiveSpacing(_containerWidth, Spacing);

    private void UpdateEstimatedRowHeight()
    {
        var cols = CalculateColumnCount();
        var spacing = GetEffectiveSpacing();
        var actualItemWidth = _containerWidth > 0
            ? (_containerWidth - (cols - 1) * spacing) / cols
            : ItemWidth;

        if (IsCompactGrid)
        {
            const int compactRowGap = 8;
            var compactFooterHeight = Math.Max(FooterHeight, 56);
            _estimatedRowHeight = MathF.Floor(actualItemWidth * AspectRatio) + compactFooterHeight + compactRowGap;
            return;
        }

        _estimatedRowHeight = (float)Math.Floor(actualItemWidth * AspectRatio) + FooterHeight + spacing;
    }

    private bool IsCompactGrid => _containerWidth > 0 && _containerWidth < VirtualGridLayout.CompactBreakpoint;

    private int EffectiveSpacing => GetEffectiveSpacing();

    private string GridCssVars =>
        FormattableString.Invariant(
            $"--item-width: {ItemWidth}px; --grid-gap: {EffectiveSpacing}px; --row-height: {_estimatedRowHeight}px; --item-aspect: {AspectRatio.ToString(CultureInfo.InvariantCulture)}; --item-ratio: {MediaCardLayout.CssRatio(PlaceholderVariant)};");

    private string GetRowGridStyle(bool includeHeight = true)
    {
        var cols = CalculateColumnCount();
        var columns = $"grid-template-columns: repeat({cols}, minmax(0, 1fr));";
        return includeHeight
            ? $"{columns} height: {_estimatedRowHeight}px;"
            : columns;
    }

    private bool HasContent() =>
        Items is { Count: > 0 } || ItemsProvider is not null;

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        if (_module is not null)
        {
            try
            {
                if (_observing)
                {
                    await _module.InvokeVoidAsync("dispose", _gridRef);
                    await _module.InvokeVoidAsync("disposeGridKeyNav", _gridRef);
                }

                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _module = null;
        _dotnetRef?.Dispose();
        _dotnetRef = null;
    }

    private object GetCellSlotKey(List<TItem> row, int column)
    {
        if (row is IndexedRow indexed)
            return indexed.RowIndex * CalculateColumnCount() + column;

        return CellKey(row[column]);
    }

    private static int GetRowIndex(List<TItem> row) =>
        row is IndexedRow indexed ? indexed.RowIndex : 0;

    private void SyncKeyNavExtent()
    {
        if (_module is null)
            return;

        _ = SyncKeyNavExtentAsync();
    }

    private async Task SyncKeyNavExtentAsync()
    {
        if (_module is null)
            return;

        try
        {
            await _module.InvokeVoidAsync(
                "setVirtualKeyNavExtent",
                _gridRef,
                _estimatedRowHeight,
                _lastTotalRows);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or InvalidOperationException)
        {
        }
    }

    private static object CellKey(TItem item) =>
        item is UnloadedBrowseItem unloaded ? unloaded.SlotIndex : item!;

    private sealed class IndexedRow : List<TItem>
    {
        public int RowIndex { get; }

        public IndexedRow(int rowIndex, IEnumerable<TItem> items)
            : base(items) =>
            RowIndex = rowIndex;
    }

    private sealed class CachedProviderRows
    {
        public int RowStart { get; init; }
        public int Cols { get; init; }
        public List<List<TItem>> Rows { get; init; } = [];
        public int TotalRows { get; init; }
    }
}
