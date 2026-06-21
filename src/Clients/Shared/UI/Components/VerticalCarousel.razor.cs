using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class VerticalCarousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private int _lastSlideCount = -1;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _module ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/K7.Clients.Shared.UI/js/vertical-carousel.js");

        if (firstRender)
        {
            await _module.InvokeVoidAsync("init", _root);
            _lastSlideCount = await _module.InvokeAsync<int>("getSlideCount", _root);
            return;
        }

        var slideCount = await _module.InvokeAsync<int>("getSlideCount", _root);
        if (slideCount != _lastSlideCount)
        {
            _lastSlideCount = slideCount;
            await _module.InvokeVoidAsync("refresh", _root);
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
}
