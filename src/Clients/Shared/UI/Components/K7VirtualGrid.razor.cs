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
    [Parameter] public RenderFragment? Placeholder { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public int ItemWidth { get; set; } = 160;
    [Parameter] public int Spacing { get; set; } = 6;
    [Parameter] public float AspectRatio { get; set; } = 1.5f;
    [Parameter] public int FooterHeight { get; set; } = 44;
    [Parameter] public int OverscanCount { get; set; } = 5;
    [Parameter] public bool SingleColumnOnMobile { get; set; }

    private ElementReference _gridRef;
    private Virtualize<List<TItem>>? _virtualizeRef;
    private readonly Dictionary<string, ItemsProviderResult<List<TItem>>> _rowsCache = new();
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
                _lastColumnCount = CalculateColumnCount();
                StateHasChanged();
            }

            await _module.InvokeVoidAsync("initGridKeyNav", _gridRef, _estimatedRowHeight);
        }
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
            return cached;
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
                .Select(chunk => chunk.ToList())
                .ToList();

            var totalRows = (int)Math.Ceiling((double)result.TotalItemCount / cols);
            _lastColumnCount = cols;
            _lastTotalRows = totalRows;

            var providerResult = new ItemsProviderResult<List<TItem>>(rows, totalRows);
            _rowsCache[cacheKey] = providerResult;
            return providerResult;
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            return default;
        }
    }

    private void RebuildRows()
    {
        if (Items is null || Items.Count == 0)
        {
            _rows = [];
            return;
        }

        var cols = CalculateColumnCount();
        _lastColumnCount = cols;
        _rows = Items
            .Chunk(cols)
            .Select(chunk => chunk.ToList())
            .ToList();
    }

    private int CalculateColumnCount()
    {
        if (SingleColumnOnMobile
            && _containerWidth > 0
            && _containerWidth < VirtualGridLayout.CompactBreakpoint)
        {
            return 1;
        }

        return VirtualGridLayout.CalculateColumnCount(_containerWidth, ItemWidth, Spacing, AspectRatio);
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

    private string GetRowGridStyle()
    {
        var cols = CalculateColumnCount();
        return $"grid-template-columns: repeat({cols}, minmax(0, 1fr)); height: {_estimatedRowHeight}px;";
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
        }

        _dotnetRef?.Dispose();
    }
}
