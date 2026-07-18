using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Web.Services;

/// <summary>
/// ICastService implementation for Blazor WebAssembly using Google Cast SDK via JS interop.
/// </summary>
internal sealed class WebCastService : ICastService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<WebCastService>? _dotNetRef;
    private readonly List<CastDeviceInfo> _devices = [];

    public bool IsAvailable { get; private set; }
    public bool IsCasting { get; private set; }
    public IReadOnlyList<CastDeviceInfo> DiscoveredDevices => _devices;

    public event Action? StateChanged;
#pragma warning disable CS0067 // Event is never used - required by ICastService interface, discovery not applicable for web
    public event Action<IReadOnlyList<CastDeviceInfo>>? DevicesDiscovered;
#pragma warning restore CS0067
    public event Action<CastMediaStatus>? MediaStatusUpdated;

    public WebCastService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task StartDiscoveryAsync()
    {
        if (_dotNetRef is not null)
            await ReleaseDotNetRefAsync();

        _dotNetRef = DotNetObjectReference.Create(this);
        IsAvailable = await _jsRuntime.InvokeAsync<bool>("K7Cast.init", _dotNetRef);
        StateChanged?.Invoke();
    }

    public async Task StopDiscoveryAsync()
    {
        await ReleaseDotNetRefAsync();
        IsAvailable = false;
        IsCasting = false;
        StateChanged?.Invoke();
    }

    public async Task CastAsync(CastMediaRequest request)
    {
        var sessionStarted = await _jsRuntime.InvokeAsync<bool>("K7Cast.requestSession");
        if (!sessionStarted) return;

        await _jsRuntime.InvokeVoidAsync("K7Cast.castMedia",
            request.StreamUrl,
            request.ContentType,
            request.Title,
            request.Subtitle,
            request.ThumbnailUrl,
            request.Duration,
            request.StartPosition);
    }

    public async Task StopCastingAsync()
    {
        await _jsRuntime.InvokeVoidAsync("K7Cast.stopCasting");
        IsCasting = false;
        StateChanged?.Invoke();
    }

    public async Task SendTransportCommandAsync(CastTransportCommand command)
    {
        switch (command)
        {
            case CastTransportCommand.Play:
                await _jsRuntime.InvokeVoidAsync("K7Cast.play");
                break;
            case CastTransportCommand.Pause:
                await _jsRuntime.InvokeVoidAsync("K7Cast.pause");
                break;
            case CastTransportCommand.Stop:
                await _jsRuntime.InvokeVoidAsync("K7Cast.stop");
                break;
        }
    }

    [JSInvokable]
    public void OnCastInitialized()
    {
        IsAvailable = true;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnReceiverAvailabilityChanged(bool available)
    {
        IsAvailable = available;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnCastStateChanged(bool isCasting)
    {
        IsCasting = isCasting;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnMediaLoaded()
    {
        IsCasting = true;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnMediaError(string error)
    {
        IsCasting = false;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnMediaStatusUpdate(string state, double position, double duration, double volume)
    {
        MediaStatusUpdated?.Invoke(new CastMediaStatus(state, position, duration, volume));
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseDotNetRefAsync();
    }

    private async Task ReleaseDotNetRefAsync()
    {
        if (_dotNetRef is null)
            return;

        await _jsRuntime.InvokeVoidAsync("K7Cast.dispose");
        _dotNetRef.Dispose();
        _dotNetRef = null;
    }
}
