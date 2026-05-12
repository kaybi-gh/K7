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
    [Parameter] public int OverscanCount { get; set; } = 4;

    private ElementReference _gridRef;
    private Virtualize<List<TItem>>? _virtualizeRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<K7VirtualGrid<TItem>>? _dotnetRef;

    private int _containerWidth;
    private int _lastColumnCount;
    private float _estimatedRowHeight = 300;
    private int _lastTotalRows;
    private bool _observing;

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
        if (width == _containerWidth) return;

        var isFirstMeasure = _containerWidth == 0;
        _containerWidth = width;
        UpdateEstimatedRowHeight();

        var newCols = CalculateColumnCount();
        if (newCols == _lastColumnCount && !isFirstMeasure) return;

        if (Items is not null)
        {
            RebuildRows();
        }

        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }

        StateHasChanged();
    }

    public async Task RefreshAsync()
    {
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

        try
        {
            var cols = CalculateColumnCount();
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

            return new ItemsProviderResult<List<TItem>>(rows, totalRows);
        }
        catch (OperationCanceledException)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
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
        if (_containerWidth <= 0) return 4;
        var cols = (_containerWidth + Spacing) / (ItemWidth + Spacing);
        return Math.Max(cols, 1);
    }

    private void UpdateEstimatedRowHeight()
    {
        var cols = CalculateColumnCount();
        var actualItemWidth = _containerWidth > 0
            ? (_containerWidth - (cols - 1) * Spacing) / cols
            : ItemWidth;
        _estimatedRowHeight = (float)Math.Floor(actualItemWidth * AspectRatio) + FooterHeight + Spacing;
    }

    private bool HasContent() =>
        Items is { Count: > 0 } || ItemsProvider is not null;

    public async ValueTask DisposeAsync()
    {
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
