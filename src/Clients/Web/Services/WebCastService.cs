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
    private bool _isAvailable;
    private bool _isCasting;
    private readonly List<CastDeviceInfo> _devices = [];

    public bool IsAvailable => _isAvailable;
    public bool IsCasting => _isCasting;
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
        _dotNetRef = DotNetObjectReference.Create(this);
        _isAvailable = await _jsRuntime.InvokeAsync<bool>("K7Cast.init", _dotNetRef);
        StateChanged?.Invoke();
    }

    public Task StopDiscoveryAsync()
    {
        return _jsRuntime.InvokeVoidAsync("K7Cast.dispose").AsTask();
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
        _isCasting = false;
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
        _isAvailable = true;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnReceiverAvailabilityChanged(bool available)
    {
        _isAvailable = available;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnCastStateChanged(bool isCasting)
    {
        _isCasting = isCasting;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnMediaLoaded()
    {
        _isCasting = true;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnMediaError(string error)
    {
        _isCasting = false;
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnMediaStatusUpdate(string state, double position, double duration, double volume)
    {
        MediaStatusUpdated?.Invoke(new CastMediaStatus(state, position, duration, volume));
    }

    public async ValueTask DisposeAsync()
    {
        if (_dotNetRef is not null)
        {
            await _jsRuntime.InvokeVoidAsync("K7Cast.dispose");
            _dotNetRef.Dispose();
            _dotNetRef = null;
        }
    }
}
