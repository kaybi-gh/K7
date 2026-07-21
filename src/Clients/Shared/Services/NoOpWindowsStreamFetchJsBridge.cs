using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class NoOpWindowsStreamFetchJsBridge : IWindowsStreamFetchJsBridge
{
    public Task RegisterAsync(IJSRuntime js) => Task.CompletedTask;
}
