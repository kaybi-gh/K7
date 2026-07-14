using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class Carousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private bool _moduleLoadFailed;

    [Parameter] public bool Skeleton { get; set; } = false;
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Style { get; set; }
    [Parameter] public bool ShowLoopBack { get; set; } = true;
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await EnsureInitializedAsync();
    }

    public async Task EnsureInitializedAsync()
    {
        if (_moduleLoadFailed)
            return;

        if (_module is null)
        {
            try
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/carousel.js");
            }
            catch (JSException)
            {
                _moduleLoadFailed = true;
                return;
            }
        }

        await _module.InvokeVoidAsync("init", _root);
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
            catch (JSException)
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
