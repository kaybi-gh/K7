using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Layout;

public partial class EmptyLayout
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private IDeviceStorageService DeviceStorage { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected override void OnInitialized()
    {
        ColdStartTrace.Write("EmptyLayout-init");
        PerfTrace.Initialize(DeviceStorage.Get(PreferenceKeys.PERF_TRACE_ENABLED) == true, Navigation);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            ColdStartTrace.Write("EmptyLayout-firstRender");
            // Before any await - starts DeferredHeavy without waiting on dismissPreload JS.
            FirstPaintSignal.Signal();
            AppReadySignal.Signal();
            PlaybackAssetLoader.Prefetch(JS);
            if (PerfTrace.IsEnabled)
                await PerfTrace.SyncJsAsync(JS);
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
