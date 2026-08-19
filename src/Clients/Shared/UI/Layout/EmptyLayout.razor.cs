using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

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
            try
            {
                await JS.InvokeVoidAsync("K7.dismissPreload");
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
            {
            }
        }
    }
}
