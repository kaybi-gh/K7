using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7VirtualList<TItem>
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public ItemsProviderDelegate<TItem>? ItemsProvider { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment? Placeholder { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public float ItemHeight { get; set; } = 64;
    [Parameter] public int OverscanCount { get; set; } = 10;

    private ElementReference _listRef;
    private Virtualize<TItem>? _virtualizeRef;
    private readonly Dictionary<string, ItemsProviderResult<TItem>> _itemsCache = new();

    private string PlaceholderStyle =>
        FormattableString.Invariant($"height: {ItemHeight}px; min-height: {ItemHeight}px");

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

        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }

    public async Task ScrollToItemIndex(int itemIndex)
    {
        var scrollTop = itemIndex * ItemHeight;
        var module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");
        await module.InvokeVoidAsync("scrollTo", _listRef, scrollTop);
        await module.DisposeAsync();
    }
}
