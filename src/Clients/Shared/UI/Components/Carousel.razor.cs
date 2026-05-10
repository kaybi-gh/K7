using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class Carousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;

    [Parameter] public bool Skeleton { get; set; } = false;
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Style { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/carousel.js");

        if (_root.Id is not null)
        {
            await _module.InvokeVoidAsync("init", _root);
        }
    }

    public async Task NotifyItemsChangedAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("reInit", _root);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("destroy", _root);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    public async Task ScrollToIndexAsync(int index)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("scrollToIndex", _root, index);
        }
    }
}
