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
    [Parameter] public int OverscanCount { get; set; } = 5;

    private ElementReference _listRef;
    private Virtualize<TItem>? _virtualizeRef;

    private async ValueTask<ItemsProviderResult<TItem>> ProvideItemsAsync(ItemsProviderRequest request)
    {
        if (ItemsProvider is null) return default;
        return await ItemsProvider(request);
    }

    public async Task RefreshAsync()
    {
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
