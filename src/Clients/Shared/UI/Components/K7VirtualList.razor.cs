using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7VirtualList<TItem> : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public ItemsProviderDelegate<TItem>? ItemsProvider { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment? Placeholder { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public float ItemHeight { get; set; } = 64;
    [Parameter] public int OverscanCount { get; set; } = 5;

    private ElementReference _listRef;
    private Virtualize<IndexedListItem>? _virtualizeRef;
    private IJSObjectReference? _module;
    private readonly Dictionary<string, ItemsProviderResult<TItem>> _itemsCache = new();
    private List<IndexedListItem>? _indexedItems;
    private bool _keyNavInitialized;
    private bool _disposed;

    private string PlaceholderStyle =>
        FormattableString.Invariant($"height: {ItemHeight}px; min-height: {ItemHeight}px");

    protected override void OnParametersSet()
    {
        _indexedItems = Items?
            .Select((item, index) => new IndexedListItem(item, index))
            .ToList();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _keyNavInitialized || !HasContent())
            return;

        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");

        await _module.InvokeVoidAsync("initListKeyNav", _listRef, ItemHeight);
        _keyNavInitialized = true;
    }

    private async ValueTask<ItemsProviderResult<IndexedListItem>> ProvideIndexedItemsAsync(ItemsProviderRequest request)
    {
        if (ItemsProvider is null)
            return default;

        var result = await ProvideItemsAsync(request);
        if (result.Items is null)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            return new ItemsProviderResult<IndexedListItem>([], result.TotalItemCount);
        }

        var sourceItems = result.Items as ICollection<TItem> ?? result.Items.ToArray();
        if (sourceItems.Count == 0)
            return new ItemsProviderResult<IndexedListItem>([], result.TotalItemCount);

        var indexedItems = sourceItems
            .Select((item, localIndex) => new IndexedListItem(item, request.StartIndex + localIndex))
            .ToArray();

        return new ItemsProviderResult<IndexedListItem>(indexedItems, result.TotalItemCount);
    }

    private async ValueTask<ItemsProviderResult<TItem>> ProvideItemsAsync(ItemsProviderRequest request)
    {
        if (ItemsProvider is null) return default;

        var cacheKey = FormattableString.Invariant($"{request.StartIndex}:{request.Count}");
        if (_itemsCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var result = await ItemsProvider(request);
            _itemsCache[cacheKey] = result;
            return result;
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            return default;
        }
    }

    public async Task RefreshAsync()
    {
        _itemsCache.Clear();
        _indexedItems = Items?
            .Select((item, index) => new IndexedListItem(item, index))
            .ToList();

        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }

    public async Task ScrollToItemIndex(int itemIndex)
    {
        var scrollTop = itemIndex * ItemHeight;
        var module = _module ?? await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");
        await module.InvokeVoidAsync("scrollTo", _listRef, scrollTop);
        if (_module is null)
            await module.DisposeAsync();
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
                if (_keyNavInitialized)
                    await _module.InvokeVoidAsync("disposeListKeyNav", _listRef);

                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private sealed record IndexedListItem(TItem Item, int Index)
    {
        public bool IsAlternate => Index % 2 == 1;
    }
}
