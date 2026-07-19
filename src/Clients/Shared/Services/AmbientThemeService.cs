using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

public sealed class AmbientThemeService(IJSRuntime js) : IAmbientThemeService
{
    public async Task PlayAsync(string themeUrl, double volume = 0.25, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(themeUrl))
            return;

        try
        {
            await js.InvokeVoidAsync("K7.AmbientTheme.play", cancellationToken, themeUrl, volume);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeVoidAsync("K7.AmbientTheme.stop", cancellationToken);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
        }
    }
}
