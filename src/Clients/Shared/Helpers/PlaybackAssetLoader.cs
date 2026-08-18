using Microsoft.JSInterop;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Loads Video.js / audio player scripts after first paint on MAUI. Hosts that already
/// include those scripts (Web, DesignSystem) no-op. Call <see cref="EnsureAsync"/> before
/// any player JS; call <see cref="Prefetch"/> as soon as the first layout paints.
/// </summary>
public static class PlaybackAssetLoader
{
    public static async Task EnsureAsync(IJSRuntime js, CancellationToken cancellationToken = default)
    {
        try
        {
            await js.InvokeVoidAsync("K7.ensurePlaybackAssets", cancellationToken);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public static void Prefetch(IJSRuntime js)
    {
        if (!OperatingSystem.IsBrowser() && !OperatingSystem.IsWindows())
            return;

        EnsureAsync(js).FireAndForget();
    }
}
