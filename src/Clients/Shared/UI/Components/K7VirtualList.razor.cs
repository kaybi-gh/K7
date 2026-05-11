using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace K7.Clients.Shared.UI.Components;

public partial class K7VirtualList<TItem>
{
    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public ItemsProviderDelegate<TItem>? ItemsProvider { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public RenderFragment? Placeholder { get; set; }
    [Parameter] public RenderFragment? EmptyContent { get; set; }
    [Parameter] public float ItemHeight { get; set; } = 64;
    [Parameter] public int OverscanCount { get; set; } = 5;

    private Virtualize<TItem>? _virtualizeRef;

    public async Task RefreshAsync()
    {
        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }
}
