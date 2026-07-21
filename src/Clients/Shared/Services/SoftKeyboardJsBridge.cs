using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class SoftKeyboardJsBridge(ISoftKeyboardService keyboard) : IDisposable
{
    private DotNetObjectReference<SoftKeyboardJsBridge>? _ref;

    public async Task RegisterAsync(IJSRuntime js)
    {
        if (_ref is not null)
            return;

        _ref = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("K7.initSoftKeyboardBridge", _ref);
    }

    [JSInvokable]
    public void Show() => keyboard.Show();

    [JSInvokable]
    public void Hide() => keyboard.Hide();

    public void Dispose() => _ref?.Dispose();
}
