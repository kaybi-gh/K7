using Microsoft.JSInterop;

namespace K7.Clients.Shared.Interfaces;

public interface IWindowsStreamFetchJsBridge
{
    Task RegisterAsync(IJSRuntime js);
}
