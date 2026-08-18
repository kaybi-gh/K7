using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;

namespace K7.Clients.Shared.UI.Layout;

public partial class EmptyLayout
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            AppReadySignal.Signal();
            PlaybackAssetLoader.Prefetch(JS);
            await JS.InvokeVoidAsync("K7.dismissPreload");
        }
    }
}
